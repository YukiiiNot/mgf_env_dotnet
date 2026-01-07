# Documentation Map

Purpose  
Provide the canonical index of repository documentation.

Audience  
Engineers working in this repo.

Scope  
Links to canonical docs by bucket and topic. Does not replace the content of those docs.

Status  
Active

---

## Key Takeaways

- This page is the entry point for repo documentation.
- If a doc is missing from this map, it is not canonical.
- Use bucket sections to find the right guidance quickly.

---

## System Context

This index links all canonical documentation and reflects the current structure of the repo.

---

## Core Concepts

Use this index to navigate the canonical documentation set.

### Start here
- Getting started: [01-onboarding/overview/getting-started.md](01-onboarding/overview/getting-started.md)
- System overview: [02-architecture/overview/system-overview.md](02-architecture/overview/system-overview.md)
- Repo workflow: [05-runbooks/operations/repo-workflow.md](05-runbooks/operations/repo-workflow.md)

### Orientation
- Developer guide: [01-onboarding/workflows/dev-guide.md](01-onboarding/workflows/dev-guide.md)
- Contributing: [01-onboarding/workflows/contributing.md](01-onboarding/workflows/contributing.md)

### Architecture
- System overview: [02-architecture/overview/system-overview.md](02-architecture/overview/system-overview.md)
- Layer conventions: [02-architecture/layers/application-layer-conventions.md](02-architecture/layers/application-layer-conventions.md)
- Business concepts catalog: [02-architecture/overview/business-concepts-catalog.md](02-architecture/overview/business-concepts-catalog.md)
- Domain/persistence map: [02-architecture/persistence/domain-persistence-map.md](02-architecture/persistence/domain-persistence-map.md)
- Extension playbook: [02-architecture/layers/extension-playbook.md](02-architecture/layers/extension-playbook.md)
- Testing and verification: [02-architecture/workflows/testing-and-verification.md](02-architecture/workflows/testing-and-verification.md)
- Project shapes: [02-architecture/layers/project-shapes.md](02-architecture/layers/project-shapes.md)
- Persistence patterns: [02-architecture/persistence/persistence-patterns.md](02-architecture/persistence/persistence-patterns.md)
- Shared Dev concurrency: [02-architecture/workflows/shared-dev-concurrency.md](02-architecture/workflows/shared-dev-concurrency.md)
- Dependencies: [02-architecture/persistence/dependencies.md](02-architecture/persistence/dependencies.md)
- Roadmap: [02-architecture/overview/roadmap.md](02-architecture/overview/roadmap.md)

### Contracts
- API overview: [03-contracts/api/overview.md](03-contracts/api/overview.md)
- Events: [03-contracts/events/jobs.md](03-contracts/events/jobs.md)
- Database schema: [03-contracts/database/schema.md](03-contracts/database/schema.md)
- Square import mapping: [03-contracts/database/square-import-mapping.md](03-contracts/database/square-import-mapping.md)
- Storage templates: [03-contracts/storage/templates.md](03-contracts/storage/templates.md)
- Configuration: [03-contracts/configuration/config-reference.md](03-contracts/configuration/config-reference.md)

### Guides
- DB migrations: [04-guides/how-to/db-migrations.md](04-guides/how-to/db-migrations.md)
- Project bootstrap (runbook): [05-runbooks/operations/repo-workflow.md](05-runbooks/operations/repo-workflow.md)
- E2E email verification: [04-guides/how-to/e2e-email-verification.md](04-guides/how-to/e2e-email-verification.md)

### Runbooks
- Delivery: [05-runbooks/business-workflows/delivery.md](05-runbooks/business-workflows/delivery.md)
- Repo workflow: [05-runbooks/operations/repo-workflow.md](05-runbooks/operations/repo-workflow.md)
- Migrations CI: [05-runbooks/migrations/migrations-ci.md](05-runbooks/migrations/migrations-ci.md)

### Decisions
- ADR template: [06-decisions/templates/adr-template.md](06-decisions/templates/adr-template.md)
- ADR index: [06-decisions/index/README.md](06-decisions/index/README.md)

### Reference
- Documentation standards: [99-reference/documentation-standards.md](99-reference/documentation-standards.md)
- Doc impact matrix: [99-reference/doc-impact-matrix.md](99-reference/doc-impact-matrix.md)
- Naming rules: [99-reference/naming-rules.md](99-reference/naming-rules.md)
- Glossary: [99-reference/glossary.md](99-reference/glossary.md)
- Documentation refactor report: [99-reference/documentation-refactor-report.md](99-reference/documentation-refactor-report.md)

---

## How This Evolves Over Time

- Update links when docs move or new buckets are added.
- Add new canonical docs immediately after they are created.

---

## Common Pitfalls and Anti-Patterns

- Letting a doc exist outside this map.
- Duplicating guidance in multiple buckets.

---

## When to Change This Document

- A new canonical doc is added.
- A doc moves or is retired.
- A bucket structure changes.

---

## Related Documents

- 02-architecture/overview/system-overview.md
- 01-onboarding/workflows/dev-guide.md
- 99-reference/documentation-standards.md

---

## Appendix (Optional)

No appendix content.

---

## Metadata

Last updated: 2026-01-02  
Owner: Documentation  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Reformatted to the documentation template and refreshed index links.
