# Integrations Configuration Contract

> Contract for integration-related configuration keys and expectations.

---

## MetaData

**Purpose:** Define the configuration contract for external integrations (Dropbox, Email, Square).
**Scope:** Covers config key ownership and expectations for integrations. Excludes host wiring and provider implementation details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Integrations are configured via appsettings (including local dev config) and env vars.
- Keys must align with dev secrets policy.
- Update this doc when integration config keys or behavior change.

---

## Main Content

Source of truth: `src/Integrations/MGF.Integrations.Dropbox/**`, `src/Integrations/MGF.Integrations.Email.*`, `src/Integrations/MGF.Integrations.Square/**`, `src/Platform/MGF.Email/**`, `config/appsettings*.json`, `tools/dev-secrets/secrets.required.json`

## Scope
- Dropbox, Email, and Square integration settings are configured via appsettings and env vars.
- Keys must comply with the dev secrets allowlist and policy.

## Related configuration contracts
- Dev secrets policy: dev-secrets.md
- Environment variables: env-vars.md
- Configuration reference: config-reference.md

---

## System Context

Integration configuration keys are shared contract inputs consumed by services and tools, and must remain stable across environments.

---

## Core Concepts

- Integrations rely on configuration keys rather than hardcoded credentials.
- Secrets are local-only for dev and must never include prod or staging values.

---

## How This Evolves Over Time

- Update when new integrations are added or keys change.
- Record any new required/optional keys for dev workflows.

---

## Common Pitfalls and Anti-Patterns

- Adding integration keys without documenting their contract.
- Storing integration secrets in committed config files.

---

## When to Change This Document

- Integration config keys or required behaviors change.
- New providers or integrations are introduced.

---

## Related Documents
- dev-secrets.md
- env-vars.md
- config-reference.md

## Change Log
- 2026-01-10 - Removed user-secrets references in favor of repo-root dev config.
