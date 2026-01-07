# Dev Secrets Inventory

> Contract for allowed developer secrets and export/import policy.

---

## MetaData

**Purpose:** Define which secrets are allowed for local development and the export/import policy.
**Scope:** Covers required/optional keys and allowed patterns for dev secrets. Excludes production or staging secrets.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Dev secrets export/import is local-only; prod/staging/CI secrets are never allowed.
- The only allowed DB secret is `Database:Dev:DirectConnectionString`.
- Export/import is limited to keys listed in `tools/dev-secrets/secrets.required.json`.

---

## Main Content

Source of truth: `tools/dev-secrets/secrets.required.json`, `src/DevTools/MGF.DevSecretsCli/MGF.DevSecretsCli.csproj`

This inventory lists the developer secrets that can be exported/imported for local dev only.

## Projects with user secrets

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

Note: The worker loads configuration via MGF.Data's UserSecretsId by default, but the Worker ID is listed so local tooling can remain consistent if secrets are stored there.

## Global policy

Allowed DB key (case-insensitive, exact match required):
- `Database:Dev:DirectConnectionString`

Disallowed key patterns:
- `*Prod*`, `*Production*`, `*Staging*`, `*CI*`, `*Github*`, `*GitHub*`

## Export and import behavior

- Export includes only keys listed in `tools/dev-secrets/secrets.required.json`.
- Keys matching disallowed patterns are never exported/imported.
- DB secrets are only allowed if they match `Database:Dev:DirectConnectionString` (case-insensitive).

## Where keys are used

- **Database:** `Database:Dev:DirectConnectionString` is used by all local tools and services.
- **Dropbox:** keys are used by delivery/share-link flows.
- **Email:** keys are used by SMTP relay or Gmail API in the delivery email workflow.

No production or staging secrets should appear in local developer exports.

---

## System Context

Dev secrets define the local-only configuration boundary used by tools and service hosts without exposing production credentials.

---

## Core Concepts

- Dev secrets are constrained to a small, explicit allowlist.
- Export/import is driven by a required keys inventory and a denylist of patterns.

---

## How This Evolves Over Time

- Update when required or optional dev secrets change.
- Revisit disallowed patterns when new environments or providers are added.

---

## Common Pitfalls and Anti-Patterns

- Exporting secrets outside the required keys list.
- Introducing prod or staging keys into local exports.

---

## When to Change This Document

- The required keys inventory changes.
- Secret policy or tool behavior changes.

---

## Related Documents
- dev-secrets-tool.md
- env-vars.md
- integrations.md
- config-reference.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
