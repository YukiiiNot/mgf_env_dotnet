# Project Shapes and Buckets

This document is the canonical source for repo structure conventions. If structure changes, update this page and
`docs/00-index.md` in the same change.

## Bucket layout (src/)
- Core: domain types, contracts, IDs, and rules without IO.
- Application: use-cases and workflow orchestration (primary boundary: `src/Application/MGF.UseCases`).
- Services: runtime hosts (API, Worker) and composition roots.
- Data: persistence, EF model/config, migrations, and stores.
- Integrations: external adapters and API clients.
- Platform: shared runtime infrastructure and reusable engines.
- Operations: operational CLIs (migration, bootstrap, delivery).
- DevTools: dev-only utilities and audits.
- Ui: desktop/web UI hosts.

## Naming conventions
- Project folder: `src/<Bucket>/<ProjectName>/`.
- Project name: `MGF.<Area>` or `MGF.<Area>.<SubArea>` (match assembly name).
- Host projects: `MGF.Api`, `MGF.Worker`, `MGF.ProjectBootstrapCli` (bucket reflects host type).
- Test projects: `tests/MGF.<Project>.Tests` or `tests/MGF.<Area>.<SubArea>.Tests`.
- Avoid legacy prefixes or duplicate buckets; prefer moving into the correct bucket.

## When to create a new project?
Create a new project when you need at least one of:
- A new deployable host boundary (API/Worker/CLI/UI).
- A distinct dependency boundary (heavy or volatile packages isolated from the rest).
- A reusable engine or integration adapter that needs its own lifecycle.
- A clear ownership boundary that cannot be expressed by folders alone.

Do not create a new project just to group files; use folders and namespaces first.

## Slice checklist (structure changes)
- Choose bucket + name using this page.
- Update `MGF.sln` and ensure project references are minimal.
- Add or update signpost README files outside `/docs` (max 10 lines; link to docs).
- Update `docs/00-index.md` and any affected architecture/onboarding pages.
- Build/test to confirm no behavior change.
