# Persistence Patterns (EF Repos + Stores)

---

## MetaData

**Purpose:** Define architecture boundaries and responsibilities for this area.
**Scope:** Covers boundaries, ownership, and dependency direction. Does not include operational steps.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-06
**Review Cadence:** on major architecture change

---

## TL;DR

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

---

## Main Content

### Default: EF repositories
Use EF repositories for normal reads/writes and composable queries.
Repository interfaces live in `src/Core/MGF.Contracts/Abstractions`; implementations live in
`src/Data/MGF.Data/Data/Repositories`.

### Stores: sealed seams for procedural or atomic persistence
Use a Store when you need:
- Atomic multi-row updates or claim/update semantics.
- JSON patch operations or bulk updates.
- Procedural SQL that is difficult to express safely in EF.
- To quarantine existing raw SQL behind a focused interface.

Stores live in `src/Data/MGF.Data/Stores/<Area>` and are surfaced via interfaces in
`src/Core/MGF.Contracts/Abstractions` when hosts or use-cases need them.

### Repo vs Store checklist
Choose a repository if:
- You are reading/writing aggregate roots and can rely on EF tracking.
- You need query composition or reusable filters.

Choose a store if:
- You need deterministic SQL or a specific locking/claiming pattern.
- You are updating JSON metadata or performing bulk updates.
- You need to preserve legacy SQL semantics exactly.

### Current stores (and why)
- `Stores/Jobs`: atomic job claim/requeue/update semantics.
- `Stores/Counters`: allocation of project codes and invoice numbers (atomic sequences).
- `Stores/Delivery`: delivery metadata updates and append-style writes.
- `Stores/ProjectBootstrap`: provisioning run metadata + storage root upsert.

Other SQL seams should follow the same rules even if not yet under `Stores/` (for example, the Square webhook
write path in `src/Data/MGF.Data/Data`).

---

## System Context

Architecture docs define bucket responsibilities and dependency direction.

---

## Core Concepts

This document explains the boundary and responsibilities for this area and how it fits into the bucket model.

---

## How This Evolves Over Time

- Update when bucket boundaries or dependency rules change.
- Add notes when a new project or workflow is introduced.

---

## Common Pitfalls and Anti-Patterns

- Putting workflow logic in hosts instead of UseCases.
- Introducing vendor logic outside Integrations.

---

## When to Change This Document

- Bucket ownership or dependency rules change.
- A new workflow impacts the described boundaries.

---

## Related Documents

- system-overview.md
- application-layer-conventions.md
- project-shapes.md
- persistence-patterns.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
