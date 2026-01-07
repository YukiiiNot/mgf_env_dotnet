# Shared Dev Concurrency and Workflow Locks

---

## MetaData

**Purpose:** Define the concurrency and isolation strategy for shared Dev infrastructure.
**Scope:** Shared Dev concurrency hazards, safety rails, and workflow lock strategy. Does not replace runbooks.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-06
**Review Cadence:** quarterly

---

## TL;DR

- Shared Dev uses one DB and shared storage roots; storage workflows must be serialized per project.
- UseCases acquire `IProjectWorkflowLock` (`ProjectWorkflowKinds.StorageMutation`) before storage IO; Data implements advisory locks.
- Lock busy is treated as a retryable deferral (requeue) that does not consume attempts and is not a workflow failure.
- Dev safety rails default to preview email and canonical recipients; staging is not a dev lane.
- UI surfaces job/lock state only; no workflow logic lives in UI or Services.

---

## Main Content

### Concurrency hazard audit

Storage-mutating workflows (shared storage side effects):
- Project bootstrap/provisioning (Dropbox folder creation, NAS/LucidLink root writes)
- Project delivery (LucidLink read, Dropbox write/share link updates)
- Project archive (moves/cleanup of project roots)
- Root integrity (audit/quarantine/move of root entries)
- Dropbox create project structure job (new project scaffolding)

Storage-mutation checklist (if any are true, take the lock):
- Touches Dropbox API for create/move/share operations
- Reads or writes LucidLink/NAS paths under shared roots
- Applies or verifies folder provisioning templates

DB-only workflows (no storage IO):
- Create project (projects + members + jobs)
- Update client/person/project metadata
- Enqueue jobs (bootstrap, delivery, archive, root integrity)
- Operations UI queries and job resets

Concurrency hazards:
- Multiple Workers can claim jobs for the same project concurrently.
- Parallel delivery/archive/bootstrap can race on the same storage root (cleanup, overwrites, share links).
- Root integrity quarantine can move/delete files while a delivery/bootstrap run is active.
- Status checks alone are not atomic; two jobs can pass the same readiness check concurrently.

### Dev safety rails

Default safety rails for Dev:
- Email defaults to preview provider; real providers require explicit enablement.
- Canonical recipients for Dev (single dev mailbox or allowlist).
- `AllowTestCleanup` only on test-mode runs and test projects.
- Destructive ops gated by `MGF_ENV=Dev` and explicit allow flags.

### Staging role

Staging is a pre-prod rehearsal lane:
- Do not use staging for day-to-day dev testing.
- Use real providers and credentials, but only with staged data.
- Treat staging as production-like for concurrency and audit trails.

### Workflow lock model

Lock strategy:
- Contract: `IProjectWorkflowLock` (UseCases only).
- Implementation: Postgres advisory lock keyed by `(project_id + workflow_kind)`.
- UseCases acquire before storage IO and release in `finally`.
- Lock busy => throw `ProjectWorkflowLockUnavailableException` to trigger deferral (no attempt consumption).

Lock scope:
- All storage-mutating workflows acquire `ProjectWorkflowKinds.StorageMutation` per project.
- Project workflows lock `project:{projectId}` scopes; root integrity locks `root:{providerKey}:{rootKey}` scopes.

### Developer rules of engagement

- Never run two storage workflows for the same project at the same time.
- Prefer TestMode + dev-owned test projects; avoid shared production-like roots.
- If a job is busy, requeue; do not override the lock.
- Use preview email and canonical recipients in Dev.
- Coordinate project ownership (see optional dev ownership tag below).

### Optional: dev ownership tagging (proposal only)

Add lightweight metadata for project scoping:
- `metadata.dev.owner`: developer initials or username
- `metadata.dev.purpose`: short note (test, demo, migration)

UI default filter: show only projects where `metadata.dev.owner` matches current user.

---

## System Context

Shared Dev concurrency is enforced inside Application UseCases via Contracts. Data implements the lock
mechanism; Services host long-running Workers and API endpoints; Services adapter projects call vendor Integrations for third-party IO.
This document applies to Dev, Staging, and Prod environments, but the urgency is highest in shared Dev.

---

## Core Concepts

## How This Evolves Over Time

- Keep `ProjectWorkflowKinds.StorageMutation` applied across storage workflows and extend to new ones.
- Replace advisory locks with a lease table if DB policies change.
- Add UI visibility for lock holder and last busy reason.

---

## Common Pitfalls and Anti-Patterns

- Running multiple Workers against shared Dev without locks enabled.
- Using staging as a dev sandbox or mixing dev data with staging runs.
- Bypassing `IProjectWorkflowLock` with direct SQL or Service-layer storage IO.
- Allowing test cleanup on shared roots without explicit coordination.

---

## When to Change This Document

- A new storage-mutating workflow is introduced or behavior changes.
- Lock strategy, job retry semantics, or environment policy changes.
- Dev/staging environment boundaries change.

---

## Related Documents

- system-overview.md
- application-layer-conventions.md
- workflows.md
- persistence-patterns.md
- jobs.md
- delivery.md
- e2e-email-verification.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Added shared Dev concurrency strategy and workflow lock model.
- 2026-01-02 - Updated deferral semantics, lock scope, and storage-mutation checklist.
- 2026-01-02 - Documented scope ids for project vs root integrity locks.
