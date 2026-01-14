# Config & Environment Contract

> Single authoritative environment identity for MGF.

**Purpose:** Define the canonical environment identity and enforce host-environment coherence for configuration loading.  
**Scope:** All entrypoints using MgfHostConfiguration; includes mapping rules and mismatch policy; excludes deployment pipelines and secret rotation.  
**Doc Type:** Reference  
**Status:** Active  
**Last Updated:** 2026-01-11  

---

## TL;DR

- `MGF_ENV` is the canonical environment identity (Dev/Staging/Prod).
- Host environment must match the canonical mapping when explicitly set.
- `appsettings.{HostEnv}.json` selection follows the mapped host environment.
- Dev secrets live in repo-root `config/appsettings.Development.json`; env vars are overrides/CI/prod.

---

## Main Content

### Owner
MGF Core (Hosting/Configuration)

### Non-goals
- Do not rename config keys or change provider ordering.
- Do not define deployment pipeline environment naming.
- Do not introduce new secret stores or file names.

### Canonical environment (MGF_ENV)
- **Allowed values:** `Dev`, `Staging`, `Prod`
- **Default:** `Dev` when unset (preserves current behavior)

### Host environment mapping
- `Dev` -> `Development`
- `Staging` -> `Staging`
- `Prod` -> `Production`

### Config loading behavior
- Always load `appsettings.json` (required).
- Load `appsettings.{HostEnv}.json` (optional) where `HostEnv` is derived from the mapping above.
- Environment variables load last and override JSON.

### Coherence policy
- If `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` is explicitly set and does **not** match the mapping above, startup must fail with a clear error.

### Dev secrets
- Dev secrets live in repo-root `config/appsettings.Development.json` (gitignored).
- Environment variables are for overrides, CI, or production/staging.

---

## System Context

This contract governs configuration loading for every entrypoint that uses the shared hosting bootstrap (API, Worker, DevConsole, CLIs, and tooling). It prevents silent divergence between domain environment (`MGF_ENV`) and host environment selection (`DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT`).

---

## Core Concepts

- **Canonical environment identity:** `MGF_ENV` is the source of truth for Dev/Staging/Prod behavior.
- **Host environment coherence:** Host environment must align with the canonical mapping when explicitly set.
- **Config selection:** `appsettings.{HostEnv}.json` selection is derived from the canonical mapping, not ad-hoc defaults.

---

## How This Evolves Over Time

- If a new environment is added, update the canonical mapping and guardrails together.
- If configuration loading changes, revalidate that the coherence policy still prevents drift.

---

## Common Pitfalls and Anti-Patterns

- Setting `MGF_ENV=Dev` while host env is explicitly set to `Production`.
- Relying on framework defaults for host env selection without checking canonical mapping.
- Treating `appsettings.{HostEnv}.json` selection as independent of domain environment.

---

## When to Change This Document

- The canonical environment mapping changes.
- A new host environment is introduced.
- Configuration loading order or sources change in the hosting bootstrap.

---

## Related Documents

- dev-secrets.md (Local dev config)
- system-overview.md (Architecture overview)

## Change Log

- 2026-01-11 - Initial contract definition.
