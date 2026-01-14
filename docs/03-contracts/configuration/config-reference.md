# Configuration Reference

> Contract for configuration sources, defaults, and option bindings.

---

## MetaData

**Purpose:** Document the contract for configuration sources and option bindings used by hosts and tools.
**Scope:** Covers config file locations, option bindings, and key ownership. Excludes runtime wiring details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Defaults live under `config/` and per-app `appsettings*.json`.
- Option bindings live in `src/Data/MGF.Data/Options/Options.cs`.
- Env vars override config files; local dev secrets live in config/appsettings.Development.json.

---

## Main Content

Source of truth: `config/appsettings*.json`, `src/Services/MGF.Api/appsettings*.json`, `src/Services/MGF.Worker/appsettings*.json`, `src/Data/MGF.Data/Options/Options.cs`

## Scope
- App configuration defaults live under `config/` and per-app `appsettings*.json` files.
- Local dev secrets live in `config/appsettings.Development.json` (git-ignored).
- Options bindings live in `src/Data/MGF.Data/Options/Options.cs`.

## Related configuration contracts
- Environment variables and selection: env-vars.md
- Dev secrets policy: dev-secrets.md
- Integrations configuration: integrations.md

---

## System Context

Configuration contracts define how hosts and tools load settings in a consistent, shared way.

---

## Core Concepts

- Configuration files provide committed defaults; secrets and environment-specific values are layered on top.
- Option bindings in MGF.Data are the contract surface for configuration consumers.

---

## How This Evolves Over Time

- Update when new option bindings are introduced or defaults change.
- Capture new config file locations or precedence changes.

---

## Common Pitfalls and Anti-Patterns

- Adding config keys without documenting their contract or defaults.
- Hardcoding secrets in appsettings or source.

---

## When to Change This Document

- New configuration sources are introduced.
- Option bindings or default config files change.

---

## Related Documents
- env-vars.md
- dev-secrets.md
- integrations.md

## Change Log
- 2026-01-10 - Updated dev secrets source of truth to repo-root config file.
