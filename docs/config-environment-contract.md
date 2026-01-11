# Config & Environment Contract

> Single authoritative environment identity for MGF.

---

## MetaData

**Purpose:** Define the authoritative environment identity and how host environments must align.
**Scope:** Configuration loading and environment coherence across all entrypoints.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-11

---

## TL;DR

- `MGF_ENV` is the canonical environment identity (Dev/Staging/Prod).
- Host environment must match the canonical mapping or startup fails when explicitly set.
- `appsettings.{HostEnv}.json` is optional and is chosen based on host env.
- Dev secrets live in `config/appsettings.Development.json` (ignored); env vars override in CI/prod.

---

## Main Content

### Canonical environment (MGF_ENV)
- **Allowed values:** `Dev`, `Staging`, `Prod`
- **Default:** `Dev` when unset (preserves current behavior)

### Host environment mapping
- `Dev` → `Development`
- `Staging` → `Staging`
- `Prod` → `Production`

### Config loading behavior
- Always load `appsettings.json` (required).
- Load `appsettings.{HostEnv}.json` (optional) where `HostEnv` is from the mapping above.
- Environment variables load last and override JSON.

### Coherence policy
- If `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` is explicitly set and does **not** match the mapping above, startup must fail with a clear error.

### Dev secrets
- `config/appsettings.Development.json` is the dev-only secrets file (gitignored).
- Environment variables are for overrides, CI, or production/staging.

---

## Related Documents

- Local dev config: [03-contracts/configuration/dev-secrets.md](03-contracts/configuration/dev-secrets.md)

## Change Log

- 2026-01-11 - Initial contract definition.
