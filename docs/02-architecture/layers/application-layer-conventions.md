# Core, Application, Services Conventions

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

### Prior content (preserved for reference)

This doc defines where workflow code lives and how runtime hosts should depend on it.

## Rule of ownership
- Services host workflows; Application owns the workflows; Data owns persistence; Integrations own external adapters.
- Application (UseCases) depends on Contracts abstractions; Data implements those Contracts.
- Persistence patterns and store rules live in persistence-patterns.md.

## Scope definitions
- Core (`src/Core/`): domain types, contracts, IDs, and shared rules that have no IO.
- Application (`src/Application/`): use-cases and workflow implementations that orchestrate domain logic. Primary boundary project: `src/Application/MGF.UseCases`.
- Services (`src/Services/`): runtime hosts (API, Worker) that call Application and wire dependencies.
- Data (`src/Data/`): persistence, EF model/config, migrations, and seeding.
- Integrations (`src/Integrations/`): external adapters (Square, Dropbox, email providers).

## Examples (current state)
- `src/Services/MGF.Api`: HTTP host; controllers and middleware are service concerns today.
- `src/Services/MGF.Worker`: job runner; many workflow handlers currently live in Worker.
- `src/Operations/MGF.ProjectBootstrapCli`: CLI orchestrator for provisioning/delivery flows.
- `src/Core/MGF.Contracts`: current home for application abstractions and shared workflow helpers.
- `src/Core/MGF.Domain`: domain entities and IDs.
- `src/Application/MGF.UseCases`: workflow/use-case boundary (scaffold; logic moves here over time).
- Project bootstrap: Worker -> `IBootstrapProjectUseCase` -> Contracts store -> Data + `IProjectBootstrapProvisioningGateway` (Services adapter: `src/Services/MGF.Worker/Adapters/Storage/ProjectBootstrap/`).

## Future direction
- New workflows and orchestration logic go in `src/Application/MGF.UseCases`; Services and Operations call into it.
- Services stay thin: hosting, composition root, and transport concerns only.
- Data stays the single owner of persistence and migrations.
- Integrations isolate external APIs and adapters behind interfaces.
- Webhook controllers validate/parse requests, then call use-cases; persistence lives behind Contracts/Data.
- API read endpoints (e.g., people listing) should call use-cases that depend on Contracts stores.

## Use-case boundary (MGF.UseCases)
MGF.UseCases is the boundary project for business workflows. See project-shapes.md for
placement rules and naming conventions.

## Checklist: When adding a new feature, place code in
- If it defines domain types or contracts, put it in `src/Core/`.
- If it implements a workflow or use-case, put it in `src/Application/MGF.UseCases`.
- If it hosts HTTP endpoints or background loops, put it in `src/Services/`.
- If it is a CLI or ops runner, put it in `src/Operations/`.
- If it persists data or defines schema, put it in `src/Data/`.
- If it integrates external APIs, put it in `src/Integrations/`.
- If it is shared runtime glue (config/logging), put it in `src/Platform/`.
- If it is UI, put it in `src/Ui/`.
- If it is dev-only tooling, put it in `src/DevTools/`.
- If unsure, start in `src/Application/` and keep Services thin.

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
