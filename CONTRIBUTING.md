# Contributing

Purpose  
Provide a signpost for contribution workflow, guardrails, and where to find canonical guidance.

Audience  
Engineers and operators contributing changes to this repo.

Scope  
High-level contribution workflow, testing expectations, and doc rules. Does not replace /docs.

Status  
Active

---

## Key Takeaways

- Canonical documentation lives under `/docs` (see [docs/00-index.md](./docs/00-index.md)).
- Respect bucket boundaries and dependency direction (see [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md)).
- UseCases are persistence-ignorant; Data owns persistence; Services host; Integrations are vendor-only IO; Platform is reusable infra.
- Call out protected infrastructure changes and request additional review.
- Run `dotnet build` and `dotnet test` (net10.0) before PR; use runbooks for e2e verification.

---

## System Context

This repo is organized into buckets with strict dependency direction and workflow guardrails.
Contributions should preserve UseCase boundaries, storage mutation safety, and contract stability.
See [docs/02-architecture/system-overview.md](./docs/02-architecture/system-overview.md) and
[docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md).

---

## Core Concepts

### Contribution Workflow (High Level)

- Keep PRs focused and link to relevant `/docs` entries.
- Update tests and runbooks when behavior changes.
- Note any protected infrastructure impact in the PR description.

Related docs: [docs/05-runbooks/repo-workflow.md](./docs/05-runbooks/repo-workflow.md), [docs/01-onboarding/contributing.md](./docs/01-onboarding/contributing.md).

### Where Changes Belong (Buckets)

UseCases orchestrate workflows and depend only on Contracts. Data implements persistence and
stores. Services host API/Worker runtimes (Worker is dispatcher only). Integrations are vendor-only
third-party IO. Platform provides reusable infrastructure utilities. Operations CLIs call UseCases.

Related docs: [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md), [docs/02-architecture/application-layer-conventions.md](./docs/02-architecture/application-layer-conventions.md), [docs/02-architecture/persistence-patterns.md](./docs/02-architecture/persistence-patterns.md).

### Protected Infrastructure Areas (Concept)

Treat changes that define long-lived contracts or workflow semantics as protected infrastructure:
- Contracts and schemas under `/docs/03-contracts`
- Artifacts and templates under `/artifacts`
- Migrations and schema changes in Data
- Workflow stores, job payloads, and storage mutation lock semantics

Call out these changes explicitly in the PR description and request review from the appropriate owner.
Use the `Owner` metadata in docs and the doc impact matrix.

Related docs: [docs/99-reference/doc-impact-matrix.md](./docs/99-reference/doc-impact-matrix.md).

### Testing Expectations

Run the standard build and test suite before PR:

```bash
dotnet build MGF.sln -c Release
dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests
```

The filtered test run may emit a "No test matches" message for the integration test assembly; this is expected.
For verification details, see [docs/02-architecture/testing-and-verification.md](./docs/02-architecture/testing-and-verification.md).

### E2E Verification (Safe Paths)

Use runbooks and preview providers to validate workflows without side effects:
- Delivery runbook: [docs/05-runbooks/delivery.md](./docs/05-runbooks/delivery.md)
- Project bootstrap guide: [docs/04-guides/how-to/project-bootstrap.md](./docs/04-guides/how-to/project-bootstrap.md)
- Email verification: [docs/04-guides/how-to/e2e-email-verification.md](./docs/04-guides/how-to/e2e-email-verification.md)

### Secrets and Local Config

Do not commit secrets. Use the dev secrets tooling and configuration guidance:
- [docs/04-guides/how-to/dev-secrets-tool.md](./docs/04-guides/how-to/dev-secrets-tool.md)
- [docs/03-contracts/configuration/dev-secrets.md](./docs/03-contracts/configuration/dev-secrets.md)

### Documentation Rules

Root docs (`README.md`, `CONTRIBUTING.md`) are signposts only. Canonical docs must live under `/docs`,
and new canonical docs must be linked from [docs/00-index.md](./docs/00-index.md). Follow the template in
[docs/99-reference/documentation-standards.md](./docs/99-reference/documentation-standards.md).

---

## How This Evolves Over Time

- Update links when the doc index or runbooks change.
- Refine protected infrastructure guidance as new contracts are introduced.

---

## Common Pitfalls and Anti-Patterns

- Bypassing UseCase boundaries or calling persistence directly from Services/UI.
- Adding vendor-specific logic outside Integrations.
- Shipping changes without updating tests and runbooks.

---

## When to Change This Document

- The contribution workflow or test expectations change.
- Bucket boundaries or protected infrastructure areas evolve.

---

## Related Documents

- [docs/00-index.md](./docs/00-index.md)
- [docs/02-architecture/project-shapes.md](./docs/02-architecture/project-shapes.md)
- [docs/02-architecture/persistence-patterns.md](./docs/02-architecture/persistence-patterns.md)
- [docs/02-architecture/shared-dev-concurrency.md](./docs/02-architecture/shared-dev-concurrency.md)
- [docs/05-runbooks/repo-workflow.md](./docs/05-runbooks/repo-workflow.md)

---

## Appendix (Optional)

No appendix content.

---

## Metadata

Last updated: 2026-01-02  
Owner: Documentation  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Initial root CONTRIBUTING signpost.
