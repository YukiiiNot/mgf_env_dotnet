# Persistence Patterns (EF Repos + Stores)

This page defines the canonical persistence patterns. Raw SQL is confined to MGF.Data.

## Default: EF repositories
Use EF repositories for normal reads/writes and composable queries.
Repository interfaces live in `src/Core/MGF.Contracts/Abstractions`; implementations live in
`src/Data/MGF.Data/Data/Repositories`.

## Stores: sealed seams for procedural or atomic persistence
Use a Store when you need:
- Atomic multi-row updates or claim/update semantics.
- JSON patch operations or bulk updates.
- Procedural SQL that is difficult to express safely in EF.
- To quarantine existing raw SQL behind a focused interface.

Stores live in `src/Data/MGF.Data/Stores/<Area>` and are surfaced via interfaces in
`src/Core/MGF.Contracts/Abstractions` when hosts or use-cases need them.

## Repo vs Store checklist
Choose a repository if:
- You are reading/writing aggregate roots and can rely on EF tracking.
- You need query composition or reusable filters.

Choose a store if:
- You need deterministic SQL or a specific locking/claiming pattern.
- You are updating JSON metadata or performing bulk updates.
- You need to preserve legacy SQL semantics exactly.

## Current stores (and why)
- `Stores/Jobs`: atomic job claim/requeue/update semantics.
- `Stores/Counters`: allocation of project codes and invoice numbers (atomic sequences).
- `Stores/Delivery`: delivery metadata updates and append-style writes.
- `Stores/ProjectBootstrap`: provisioning run metadata + storage root upsert.

Other SQL seams should follow the same rules even if not yet under `Stores/` (for example, the Square webhook
write path in `src/Data/MGF.Data/Data`).
