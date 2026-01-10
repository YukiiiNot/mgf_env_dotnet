# Desktop UI Template

> Standard structure for WPF desktop executables and shared UI assets.

---

## MetaData

**Purpose:** Define the canonical folder template for desktop UI executables and shared WPF assets.
**Scope:** Covers folder structure, ownership boundaries, and hard rules for desktop UI projects. Excludes feature implementation guidance.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-10

---

## TL;DR

- Every desktop executable follows a fixed folder layout.
- Shared UI assets live in MGF.Desktop.Shared, not in app-specific folders.
- UI projects must not reference Data or EF/Npgsql.

---

## Main Content

### Executable template (MGF.<Tool>.Desktop)

Top-level folders that must exist in each desktop executable:

```
src/Ui/MGF.<Tool>.Desktop/
  Hosting/
  Api/
  Modules/
  Resources/
  Diagnostics/
```

#### Hosting
- App startup, host builder, and DI composition root.
- WPF App.xaml/App.xaml.cs and host configuration.
- All HTTP client wiring lives here (base URL, headers, timeouts).
- Must not contain feature UI or business logic.

#### Api
- API client adapters and DTO mapping for this tool.
- No WPF types; keep transport and serialization concerns only.
- Api clients are DI-managed typed HttpClients configured in Hosting.
- Api clients must not read IConfiguration or set headers.
- Only depends on contracts and shared HTTP abstractions.

#### Modules
- Tool-specific UI composition (Windows, Views, ViewModels).
- Feature wiring and UI flow for this executable only.
- Must not contain API client implementation.

Polling lifecycle ownership:
- Hosting wires Start/Stop for polling ViewModels.
- ViewModels must not auto-start in constructors.
- Poll intervals live in the ViewModel as explicit TimeSpan values.
- Status module is the canonical example.

#### Resources
- App-specific resource dictionaries, styles, and theme overrides.
- Localized strings or visual assets scoped to this tool.
- Do not put shared styles here.

#### Diagnostics
- Logging, tracing, diagnostics views, and health panels.
- Dev-only debug surfaces or operational readouts.
- Must not depend on Data or EF/Npgsql.

### Shared library (MGF.Desktop.Shared)

MGF.Desktop.Shared is for reusable WPF assets only:

- Shared Views, Controls, Styles, and Resources.
- Reusable UI primitives that are tool-agnostic.
- No API clients, no tool-specific modules, no hosting logic.

### Hard rules

- UI projects must not reference MGF.Data, EF Core, or Npgsql.
- Api folder must not depend on WPF types.
- Hosting is the only place for DI registration.
- Hosting configures Api:BaseUrl, Security:ApiKey, and optional Security:Operator headers.
- Shared is for reusable UI assets only; it must not own tool-specific workflows.

### First read-only slice pattern

- Banner: Local MGF_ENV + Api:BaseUrl + API MGF_ENV (from /api/meta).
- Connectivity line: Connected/Disconnected with last error.
- Polling guidance: list surfaces every 2-5 seconds; detail panels 5-10 seconds.
- Keep polling in a ViewModel; keep transport in Api clients.

### Jobs module precedent

- List + detail via polling + selection fetch is the default pattern.
- List endpoint excludes payload; detail endpoint includes payload.
- Payload display is opt-in; hidden by default until the operator requests it.
- Payload display must truncate at 50 KB and append "...(truncated)".
- Default "last 24 hours" window is based on created_at (job creation time).
- run_after, started_at, finished_at are informational only and do not affect inclusion.
- Rationale: created_at is stable/immutable, keeps pagination stable, matches operator intent.

### API list endpoints for polled UIs

- List endpoints MUST be bounded by default.
- List endpoints MUST NOT include payload blobs.
- Cursor pagination preferred for polled surfaces.

---

## System Context

Desktop executables are thin hosts that compose shared UI assets and tool-specific workflows without owning domain logic.

---

## Core Concepts

- Executables are small, explicit, and composable.
- Shared UI assets reduce duplication while keeping tool policy isolated.

---

## How This Evolves Over Time

- Add new tools by cloning the executable template and wiring modules.
- Extend shared assets only when multiple tools reuse the same WPF surface.

---

## Common Pitfalls and Anti-Patterns

- Putting API client logic in Shared.
- Registering services outside Hosting.
- Adding Data/EF references in UI projects.

---

## When to Change This Document

- The executable folder template changes.
- Shared vs exe ownership boundaries change.

---

## Related Documents

- project-shapes.md
- application-layer-conventions.md

## Change Log

- 2026-01-07 - Initial template definition.
