# Contributing

Purpose  
Explain how we make changes safely in a production-adjacent repo: where contributions go, what guardrails exist, and how to avoid breaking long-lived contracts.

Audience  
All contributors (new devs, experienced devs, operators).

Scope  
Covers contribution expectations, review expectations, and “what to check before you change something.” Does not document specific workflows end-to-end (runbooks do that) and does not define architecture rules in full detail (architecture docs do that).

Key takeaways
- Changes should be small, scoped, and aligned with the bucket boundaries (UseCases/Contracts/Data/Services/etc.).
- Some areas represent long-lived contracts; changes there require extra care and usually an explicit review.
- If you touch a workflow, update the tests and the relevant runbook/docs page that describes how to operate it.
- Prefer adding guardrails (tests/docs/contracts) over adding “quick local overrides.”

## How to contribute safely

1) Choose the right “bucket”
- Business workflows and orchestration belong in UseCases (Application) behind Contracts.
- Persistence belongs in Data behind Contracts interfaces.
- Hosts (API/Worker/Operations) should be thin adapters that call UseCases.
- Integrations is vendor-only (3rd party APIs). Non-vendor system logic belongs in Platform or Services adapters.

If you’re unsure where something belongs, start here:  
- ../02-architecture/project-shapes.md  
- ../02-architecture/extension-playbook.md

2) Keep PRs small and reviewable
- One primary intent per PR (move/rename, boundary routing, add a workflow, etc.).
- If the PR changes behavior, it must say so explicitly and include evidence/tests.
- If the PR is mechanical (move/rename), keep it purely mechanical.

3) When you change a workflow
A “workflow change” includes any change that affects:
- job payload shapes, job types, job status transitions
- email composition/sending policy
- folder provisioning logic and templates/schemas
- storage root contracts / verification
- persistence that writes project/client/status state

Minimum expectations:
- Update or add tests covering the changed behavior.
- Update the relevant runbook(s) under ../05-runbooks/.
- Update architecture docs only if the architecture changed (not just paths).

## Protected contract areas (extra care + review expected)

These areas define long-lived contracts that many workflows depend on. Changes here should be treated as “infra contract changes,” and should typically get an explicit review from the repo infra owner / maintainers.

- artifacts/templates/** and artifacts/schemas/** (folder templates and schema contracts)
- docs/03-contracts/** (published contracts)
- src/Platform/** (cross-cutting infrastructure and system components)
- src/Data/** (migrations, stores, persistence semantics)
- job definitions/payload models and status transitions (often in Contracts + UseCases)
- any code that provisions, validates, repairs, or bootstraps storage containers

Why: these surfaces shape the system’s “physics.” Drift here breaks many things.

## “Usually safe” areas (still use good judgment)

These changes are often lower risk, but still must follow boundaries and tests:
- UI features and presentation changes
- new read-only queries
- isolated unit tests or refactors inside a bucket with no behavior change
- documentation improvements (under /docs)

## Required checks before opening a PR

- dotnet build MGF.sln -c Release
- dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests
- Confirm architecture rules still pass (Architecture tests run in the test suite)
- If relevant, follow the verification steps in ../05-runbooks/ and/or ../02-architecture/testing-and-verification.md

Related docs
- ../01-onboarding/getting-started.md
- ../01-onboarding/dev-guide.md
- ../02-architecture/project-shapes.md
- ../02-architecture/domain-persistence-map.md
- ../02-architecture/testing-and-verification.md
- ../05-runbooks/ (runbooks index)

Last updated: 2026-01-02  
Owner: Repo maintainers / Infra owner  
Status: Draft
