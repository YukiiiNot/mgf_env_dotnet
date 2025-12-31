# Documentation Map

This is the entry point for repo documentation. Every doc should be reachable from here.

## Start here
- Getting started: [01-onboarding/getting-started.md](01-onboarding/getting-started.md)
- System overview: [02-architecture/system-overview.md](02-architecture/system-overview.md)
- Repo workflow: [05-runbooks/repo-workflow.md](05-runbooks/repo-workflow.md)
- Delivery runbook: [05-runbooks/delivery.md](05-runbooks/delivery.md)
- DB migrations how-to: [04-guides/how-to/db-migrations.md](04-guides/how-to/db-migrations.md)
- Infrastructure contracts: [03-contracts/storage/infra-contracts.md](03-contracts/storage/infra-contracts.md)
- API contract: [03-contracts/api/overview.md](03-contracts/api/overview.md)

## Orientation
- Getting started: [01-onboarding/getting-started.md](01-onboarding/getting-started.md)
- Developer guide: [01-onboarding/dev-guide.md](01-onboarding/dev-guide.md)
- Contributing: [01-onboarding/contributing.md](01-onboarding/contributing.md)

## Architecture
- System overview: [02-architecture/system-overview.md](02-architecture/system-overview.md)
- Layer conventions: [02-architecture/application-layer-conventions.md](02-architecture/application-layer-conventions.md)
- Workflows: [02-architecture/workflows.md](02-architecture/workflows.md)
- Database phase 1: [02-architecture/database-phase1.md](02-architecture/database-phase1.md)
- Roadmap: [02-architecture/roadmap.md](02-architecture/roadmap.md)
- Dependencies: [02-architecture/dependencies.md](02-architecture/dependencies.md)

## Contracts
### API
- Overview: [03-contracts/api/overview.md](03-contracts/api/overview.md)
- HTTP examples: [03-contracts/api/http-examples.http](03-contracts/api/http-examples.http)

### Events
- Square webhooks: [03-contracts/events/square-webhooks.md](03-contracts/events/square-webhooks.md)
- Email: [03-contracts/events/email.md](03-contracts/events/email.md)
- Jobs: [03-contracts/events/jobs.md](03-contracts/events/jobs.md)

### Database
- Schema: [03-contracts/database/schema.md](03-contracts/database/schema.md)
- Migrations: [03-contracts/database/migrations.md](03-contracts/database/migrations.md)
- Schema CSVs: [03-contracts/database/schema-csv/README.md](03-contracts/database/schema-csv/README.md)
- Square import mapping: [03-contracts/database/square-import-mapping.md](03-contracts/database/square-import-mapping.md)

### Configuration
- Config reference: [03-contracts/configuration/config-reference.md](03-contracts/configuration/config-reference.md)
- Env vars: [03-contracts/configuration/env-vars.md](03-contracts/configuration/env-vars.md)
- Dev secrets: [03-contracts/configuration/dev-secrets.md](03-contracts/configuration/dev-secrets.md)
- Integrations: [03-contracts/configuration/integrations.md](03-contracts/configuration/integrations.md)

### Storage
- Templates: [03-contracts/storage/templates.md](03-contracts/storage/templates.md)
- Containers: [03-contracts/storage/containers.md](03-contracts/storage/containers.md)
- Infra contracts: [03-contracts/storage/infra-contracts.md](03-contracts/storage/infra-contracts.md)
- Schema reference: [03-contracts/storage/schema-reference.md](03-contracts/storage/schema-reference.md)

## Guides
### How-to
- DB migrations: [04-guides/how-to/db-migrations.md](04-guides/how-to/db-migrations.md)
- Project bootstrap: [04-guides/how-to/project-bootstrap.md](04-guides/how-to/project-bootstrap.md)
- Square import: [04-guides/how-to/square-import.md](04-guides/how-to/square-import.md)
- Dev secrets tool: [04-guides/how-to/dev-secrets-tool.md](04-guides/how-to/dev-secrets-tool.md)
- Legacy audit: [04-guides/how-to/legacy-audit.md](04-guides/how-to/legacy-audit.md)

### Troubleshooting
- Delivery: [04-guides/troubleshooting/delivery.md](04-guides/troubleshooting/delivery.md)
- Integrations: [04-guides/troubleshooting/integrations.md](04-guides/troubleshooting/integrations.md)

## Runbooks
- Delivery: [05-runbooks/delivery.md](05-runbooks/delivery.md)
- Repo workflow: [05-runbooks/repo-workflow.md](05-runbooks/repo-workflow.md)
- Destructive ops audit: [05-runbooks/destructive-ops-audit.md](05-runbooks/destructive-ops-audit.md)
- Migrations CI: [05-runbooks/migrations-ci.md](05-runbooks/migrations-ci.md)

## Decisions
- ADR index: [06-decisions/README.md](06-decisions/README.md)

## Reference
- Glossary: [99-reference/glossary.md](99-reference/glossary.md)
- Naming rules: [99-reference/naming-rules.md](99-reference/naming-rules.md)
- Style guide: [99-reference/style-guide.md](99-reference/style-guide.md)
- Doc impact matrix: [99-reference/doc-impact-matrix.md](99-reference/doc-impact-matrix.md)

## Canonical artifacts (paths stay stable)
- Templates: [../artifacts/templates/](../artifacts/templates/)
- Schemas: [../artifacts/schemas/](../artifacts/schemas/)
- Schema CSVs: [db_design/schema_csv/README.md](db_design/schema_csv/README.md)
