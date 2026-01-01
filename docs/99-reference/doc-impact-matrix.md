# Doc Impact Matrix

Use this matrix to decide which docs must change when code or config changes.

| Signal (what changed) | Docs to update | Automation potential |
| --- | --- | --- |
| `src/Services/MGF.Api/Controllers/*.cs`, `src/Services/MGF.Api/Program.cs` | `docs/03-contracts/api/overview.md`, `docs/03-contracts/api/http-examples.http` | Medium (OpenAPI or .http generation) |
| `src/Services/MGF.Api/Controllers/SquareWebhooksController.cs`, `src/Services/MGF.Api/Square/**` | `docs/03-contracts/events/square-webhooks.md`, `docs/03-contracts/configuration/integrations.md` | Low (manual, payload examples) |
| `src/Services/MGF.Worker/**` (job handlers, email, delivery) | `docs/03-contracts/events/jobs.md`, `docs/03-contracts/events/email.md`, `docs/05-runbooks/delivery.md` | Low |
| `src/Data/MGF.Data/Migrations/*.cs`, `src/Data/MGF.Data/Migrations/AppDbContextModelSnapshot.cs` | `docs/03-contracts/database/schema.md`, `docs/03-contracts/database/migrations.md`, `docs/04-guides/how-to/db-migrations.md` | Medium (schema export) |
| `artifacts/templates/*.json`, `artifacts/schemas/*.schema.json` | `docs/03-contracts/storage/templates.md`, `docs/03-contracts/storage/containers.md`, `docs/03-contracts/storage/schema-reference.md` | Medium (schema/doc generation) |
| `config/appsettings*.json`, `src/Services/MGF.Api/appsettings*.json`, `src/Services/MGF.Worker/appsettings*.json`, `src/Data/MGF.Data/Options/Options.cs` | `docs/03-contracts/configuration/config-reference.md`, `docs/03-contracts/configuration/env-vars.md` | Medium (options + JSON reference) |
| `tools/dev-secrets/secrets.required.json`, `tools/dev-secrets/src/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj` | `docs/03-contracts/configuration/dev-secrets.md`, `docs/04-guides/how-to/dev-secrets-tool.md` | Medium (generate from JSON) |
| `src/DevTools/MGF.SquareImportCli/**` | `docs/03-contracts/database/square-import-mapping.md`, `docs/04-guides/how-to/square-import.md` | Low |
| `.github/workflows/*.yml`, `scripts/*.ps1` | `docs/05-runbooks/repo-workflow.md`, `docs/05-runbooks/migrations-ci.md` | Low |
| `docs/db_design/schema_csv/**` | `docs/03-contracts/database/schema-csv/README.md`, `docs/03-contracts/database/schema.md` | Low (design docs) |



