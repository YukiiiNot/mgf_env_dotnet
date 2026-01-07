# Contributing

> How to make safe, reviewable changes in a production-adjacent repo.

---

## MetaData

**Purpose:** Explain how contributions should be scoped and reviewed to protect long-lived contracts and workflows for all contributors (new devs, experienced devs, operators).
**Scope:** Contribution expectations, review expectations, and what to check before changing behavior. Does not replace runbooks or architecture rules in full detail.
**Doc Type:** Guide
**Status:** Active
**Last Updated:** 2026-01-06

---

## TL;DR

- Keep PRs small and aligned to bucket boundaries.
- Treat contract areas as protected and update tests/runbooks when workflows change.
- Run the required build/test checks before opening a PR.

---

## Main Content

*How to contribute safely*

1) Choose the right bucket  
Use the canonical bucket map and ownership rules:
- project-shapes.md
- application-layer-conventions.md
- extension-playbook.md

2) Keep PRs small and reviewable
- One primary intent per PR (move/rename, boundary routing, add a workflow, etc.).
- If the PR changes behavior, say so explicitly and include evidence/tests.
- If the PR is mechanical (move/rename), keep it purely mechanical.

3) When you change a workflow
A workflow change includes any change that affects:
- job payload shapes, job types, or job status transitions
- email composition or sending policy
- folder provisioning logic and templates/schemas
- storage root contracts and verification
- persistence that writes project/client/status state

Minimum expectations:
- Update or add tests covering the changed behavior.
- Update the relevant runbook(s) under 05-runbooks
- Update architecture docs only if the architecture changed (not just paths).

*Protected contract areas (extra care + review expected)*

These areas define long-lived contracts that many workflows depend on. Changes here should be treated as infra contract changes and should get explicit review from maintainers.

- artifacts/templates/** and artifacts/schemas/** (folder templates and schema contracts)
- docs/03-contracts/** (published contracts)
- src/Platform/** (cross-cutting infrastructure and system components)
- src/Data/** (migrations, stores, persistence semantics)
- job definitions/payload models and status transitions (often in Contracts + UseCases)
- any code that provisions, validates, repairs, or bootstraps storage containers

Why: these surfaces shape the system's physics. Drift here breaks many things.

*Usually safe areas (still use good judgment)*

These changes are often lower risk, but still must follow boundaries and tests:
- UI features and presentation changes
- new read-only queries
- isolated unit tests or refactors inside a bucket with no behavior change
- documentation improvements (under /docs)

*Required checks before opening a PR*

- dotnet build MGF.sln -c Release
- dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests
- Confirm architecture rules still pass (Architecture tests run in the test suite)
- If relevant, follow verification steps in 05-runbooks and/or testing-and-verification.md

---

## System Context

This document is a contribution guardrail. It protects workflow stability and contract surfaces while pointing contributors to the canonical architecture and runbook sources.

---

## Core Concepts

- Bucket boundaries and contract surfaces are the primary safety rails.
- Workflow changes require test updates and operational documentation updates.
- Protected contract areas require explicit review.

---

## How This Evolves Over Time

- Update when protected contract areas change.
- Update when required checks or verification steps change.
- Add new guidance when a recurring contribution mistake appears.

---

## Common Pitfalls and Anti-Patterns

- Changing contract surfaces without explicit review.
- Skipping runbook or testing updates after workflow changes.
- Mixing mechanical refactors with behavioral changes.

---

## When to Change This Document

- A new protected contract area is introduced.
- The required checks or baseline commands change.
- Contribution expectations shift for a workflow or subsystem.

---

## Related Documents
- getting-started.md
- dev-guide.md
- project-shapes.md
- domain-persistence-map.md
- testing-and-verification.md
- 05-runbooks

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
