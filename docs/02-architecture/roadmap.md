# MGF Roadmap

Short, daily-readable map of what exists, what is partial, and what we do next.

## Workflow Coverage Map

| Workflow | Status | Key commands/jobs | Next concrete milestone |
| --- | --- | --- | --- |
| Project lifecycle (create → bootstrap/provision → active) | Done (v1) | `project.bootstrap` job, `MGF.ProjectBootstrapCli` | Reduce manual steps for new project setup (UI/ops console) |
| Archive (active → to_archive → archived) | Done (v1) | `project.archive` job | Add retention reporting (report-only) |
| Delivery (versioned exports + share + email) | Done (v1.3) | `project.delivery`, `project.delivery_email` | Delivery runs table + retention report (no deletes) |
| Asset ingest | Not Started | — | Define ingest contract + guardrails |
| Review exports / approvals loop | Not Started | — | Decide approval model (simple status + notes) |
| Billing gates (invoice issued/paid) | Partial | lookups + schema exist | Define minimal “invoice ready” gate |
| Scheduling/due dates/milestones | Partial | schema exists | Define minimal milestone model |
| Retention reporting (report-only) | Partial | delivery metadata | Add a report-only job + runbook |
| Observability (jobs dashboard) | Partial | `public.jobs` + CLI show | Add lightweight WPF/CLI dashboard |
| Client portal publishing (Notion later) | Not Started | — | Define data contract + publish trigger |
| Desktop tooling (WPF) | Not Started | — | Minimal Ops Console MVP |

## Next 30 / 60 / 90 Days

### Next 30 days (ship and stabilize)
1. Delivery runbook + operator workflow solidification
   - Acceptance: docs/05-runbooks/delivery.md is used successfully by a second operator end-to-end.
2. Delivery retention reporting (report-only)
   - Acceptance: report lists deliveries older than retention window; no deletes.
3. Root integrity audit cadence (report-only)
   - Acceptance: root-audit runs are repeatable and logged, no repairs by default.
4. WPF Ops Console MVP plan + skeleton
   - Acceptance: a scoped plan for browse/search projects + enqueue jobs.
5. Docs cohesion pass
   - Acceptance: README + ONBOARDING + ROADMAP are consistent with current CLI flags.

### Next 60 days (reduce friction)
1. Add a minimal review/approval loop
   - Acceptance: project can enter “ready_to_deliver” via a single explicit action.
2. Add delivery runs table (optional) + UI read
   - Acceptance: runs are queryable without reading JSON metadata.
3. Add a basic jobs dashboard (CLI or WPF)
   - Acceptance: show queued/running/failed jobs with clear filters.

### Next 90 days (operational polish)
1. Retention policy UI + reminders (report-only)
   - Acceptance: operators can see upcoming expirations.
2. Add ingest workflow guardrails
   - Acceptance: ingest creates deterministic container + manifest without side effects.

## Non-goals for now

- Automated .prproj merging or editing workflows
- Automated renders/exports (editor remains source of truth)
- Complex client portal features

## Working agreements (multi-dev)

- Small, focused PRs only. Avoid sweeping refactors.
- Contract-first: update docs/tests/runbooks with each workflow change.
- Avoid overlapping files. Use ownership zones:
  - Email subsystem: `src/Platform/MGF.Email/`
  - Delivery: `src/Application/MGF.UseCases/UseCases/Operations/ProjectDelivery/` + `src/Services/MGF.Worker.Adapters.Storage/ProjectDelivery/`
  - Bootstrap: `src/Application/MGF.UseCases/UseCases/Operations/ProjectBootstrap/` + `src/Services/MGF.Worker.Adapters.Storage/ProjectBootstrap/`
  - Archive: `src/Services/MGF.Worker/ProjectArchive/`
  - Templates/Contracts: `artifacts/templates/`, `docs/03-contracts/storage/infra-contracts.md`
- Communicate changes in ROADMAP + README docs index.

## WPF App (Ops Console) scope

Purpose: a safe operator shell around the existing jobs and runbooks.

First milestone (MVP):
- Authenticate (even if stubbed initially)
- Browse/search projects
- View project status + current delivery state
- Enqueue bootstrap/deliver/delivery-email jobs with guardrails
- View job queue + last errors

Defer:
- Heavy UI polish
- Client portal features
- Complex analytics dashboards
