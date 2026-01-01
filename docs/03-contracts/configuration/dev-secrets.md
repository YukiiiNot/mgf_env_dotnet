# Dev Secrets Inventory

Source of truth: `tools/dev-secrets/secrets.required.json`, `tools/dev-secrets/src/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj`
Change control: Update when required/optional keys or secret policy changes.
Last verified: 2025-12-30


This inventory lists the developer secrets that can be exported/imported for **local dev only**. The only database secret allowed is the **Dev direct connection string**: `Database:Dev:DirectConnectionString`.

> Policy: do **not** export/import any Prod/Staging/CI secrets. If a key name matches disallowed patterns (Prod/Staging/CI/GitHub), it is rejected.

## Projects with User Secrets

### MGF.Data
- **UserSecretsId:** `8f8e4093-a213-4629-bbd1-2a67c4e9000e`
- **Required keys**
  - `Database:Dev:DirectConnectionString`
    - **Why:** Required for local database access (Dev only).
    - **Example:** `Host=db.<ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...;Ssl Mode=Require;Pooling=false`
- **Optional keys** (only if you need these local workflows)
  - `Security:ApiKey` (API auth for local requests)
  - `Integrations:Dropbox:*` (share links + API root mode)
  - `Integrations:Email:*` (SMTP relay or Gmail API)

### MGF.Worker
- **UserSecretsId:** `dotnet-MGF.Worker-41014bcc-815d-45c3-8f59-a2c2649897b2`
- **Required keys**
  - `Database:Dev:DirectConnectionString`
- **Optional keys**
  - `Integrations:Dropbox:*`
  - `Integrations:Email:*`

> Note: The worker loads configuration via `MGF.Data`'s UserSecretsId by default, but we include the Worker ID so local tooling can remain consistent if secrets are stored there.

## Global Policy

Allowed DB key (case-insensitive, exact match required):
- `Database:Dev:DirectConnectionString`

Disallowed key patterns:
- `*Prod*`, `*Production*`, `*Staging*`, `*CI*`, `*Github*`, `*GitHub*`

## Export/Import Behavior

- Export includes **only** keys listed in `tools/dev-secrets/secrets.required.json`.
- Keys matching disallowed patterns are **never** exported/imported.
- DB secrets are only allowed if they are exactly `Database:Dev:DirectConnectionString` (case-insensitive).

## Where Keys Are Used

- **Database:** `Database:Dev:DirectConnectionString` is used by all local tools and services.
- **Dropbox:** keys are used by delivery/share-link flows.
- **Email:** keys are used by SMTP relay or Gmail API in the delivery email workflow.

No production or staging secrets should appear in local developer exports.



