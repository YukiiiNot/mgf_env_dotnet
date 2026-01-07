# API Contract Overview

> Contract boundary for internal API endpoints and authentication.

---

## MetaData

**Purpose:** Define the internal API contract surface and auth expectations for consumers.
**Scope:** Covers API route families and authentication headers. Excludes host wiring and runtime config details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Internal API endpoints live under `/api/*`; webhooks live under `/webhooks/*`.
- `/api/*` requires the `X-MGF-API-KEY` header.
- Update this document when routes, auth requirements, or payload shapes change.

---

## Main Content

## Scope
- Internal API for MGF apps and tools.
- Authenticated endpoints use `/api/*`.
- Webhooks use `/webhooks/*`.

## Authentication
- Requests to `/api/*` require the `X-MGF-API-KEY` header.

## References
- HTTP examples: `http-examples.http`

---

## System Context

The API contract defines the boundary between service hosts and contract consumers and must remain stable for tools and UIs.

---

## Core Concepts

- The contract is the stable API surface; implementations can evolve behind it.
- Authentication is enforced at the API boundary through a single header.

---

## How This Evolves Over Time

- Additive changes should preserve existing routes and request/response shapes.
- Breaking changes require versioning or explicit migration guidance.

---

## Common Pitfalls and Anti-Patterns

- Adding endpoint behavior without documenting the contract.
- Changing request/response shapes without compatibility planning.

---

## When to Change This Document

- Routes, authentication requirements, or documented payload shapes change.
- A new client depends on a new or changed API contract.

---

## Related Documents
- system-overview.md
- application-layer-conventions.md
- jobs.md
- env-vars.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
