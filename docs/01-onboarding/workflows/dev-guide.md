# Developer Guide

> How to add functionality without breaking boundaries or contracts.

---

## MetaData

**Purpose:** Teach contributors (developers building features, workflows, UI, integrations, and operational tooling) how to work inside the system day-to-day and keep changes safe as the system expands.
**Scope:** Development principles, boundaries, and where changes go. Does not replace runbooks or detailed architecture docs.
**Doc Type:** Guide
**Status:** Active
**Last Updated:** 2026-01-06

---

## TL;DR

- Start by choosing the right bucket and follow dependency direction rules.
- Use explicit workflows, contracts, and tests instead of hidden side effects.
- Keep integrations and email orchestration in the correct buckets.

---

## Main Content

*Working agreements (what we optimize for)*

- Guardrails over cleverness: prefer tests, contracts, and explicit seams.
- Explicit workflows: jobs and status transitions are explicit, not hidden side effects.
- Idempotency: job processing should tolerate retries safely.
- Traceability: when behavior matters, make it observable (logs, stored metadata, docs).

*Where to add things*

Use the canonical bucket map and placement rules:
- project-shapes.md
- application-layer-conventions.md

Use the extension playbook for decision-making and examples:
- extension-playbook.md

*Integrations (vendor-only)*

Add new vendor integrations under:
- src/Integrations/MGF.Integrations.<Provider>/

Rules of thumb:
- Integrations contains HTTP clients, auth/token handling, provider models, and provider-specific failure translation.
- Integrations does not contain workflow orchestration.
- Prefer Contracts abstractions for anything the rest of the system calls.

*Email (conceptual overview)*

Email spans multiple buckets:
- Platform: composition and templates (what we send and how it renders)
- Integrations: providers (how it is sent via Gmail/SMTP/etc.)
- Contracts: abstractions for senders/composers and shared models
- UseCases: when and why an email is sent (workflow orchestration)
- Services: wiring and runtime selection

For operational steps and verification, use:
- testing-and-verification.md

*Folder provisioning (conceptual overview)*

MGF.FolderProvisioning is a folder topology engine used by workflows to:
- plan, apply, and verify folder structures across storage containers
- enforce folder structure rules via policy

It is not a generic provisioning system; it is explicitly about folder trees across storage providers.

Authoritative detail:
- provisioning.md

*Testing philosophy*

- Unit tests for pure logic (planners, guards, models).
- Contract tests for must-not-drift invariants (stable delivery paths, allowlists, naming rules).
- Avoid destructive DB tests unless explicitly opted in and isolated.

---

## System Context

This guide connects onboarding to the architecture and contracts. It focuses on day-to-day development practices while deferring implementation detail to the architecture docs.

---

## Core Concepts

- UseCases orchestrate workflows; Hosts dispatch; Data persists; Integrations talk to vendors.
- Dependency direction and contract surfaces preserve consistency.
- The bucket you choose determines where a change belongs and how it is tested.

---

## How This Evolves Over Time

As MGF expands, keep bucket boundaries aligned with the canonical map and ownership rules:
- project-shapes.md
- application-layer-conventions.md
- extension-playbook.md

---

## Common Pitfalls and Anti-Patterns

- Putting workflow orchestration inside Integrations.
- Adding behavior without updating tests, contracts, or docs.
- Treating vendor models as stable core domain concepts.

---

## When to Change This Document

- Bucket boundaries or ownership rules change.
- A new subsystem or workflow pattern needs guidance here.
- The integration, email, or provisioning guidance changes materially.

---

## Related Documents
- getting-started.md
- contributing.md
- project-shapes.md
- domain-persistence-map.md
- extension-playbook.md
- 05-runbooks

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
