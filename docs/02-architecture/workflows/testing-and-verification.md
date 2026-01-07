# Testing and Verification

---

## MetaData

**Purpose:** Provide the required verification checklist for changes.
**Scope:** Build, test, and E2E verification steps for safe changes. Does not replace runbooks.
**Doc Type:** Guide
**Status:** Active
**Last Updated:** 2026-01-06

---

## TL;DR

- Run the standard build and test suite before changes land.
- Use E2E verification and relevant runbooks when workflows change.

---

## Main Content

Checklist (short form):

- Build: `dotnet build MGF.sln -c Release`
- Unit tests: `dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests`
- Architecture tests: `dotnet test tests/MGF.Architecture.Tests/MGF.Architecture.Tests.csproj -c Release`
- E2E verification: follow `e2e-email-verification.md`
- Runbook validation: if a workflow changed, follow the relevant runbook in `05-runbooks`

For step-by-step E2E email verification, use:
- e2e-email-verification.md

---

## System Context

This checklist sits in the architecture docs to enforce safe, repeatable validation before workflow changes.

---

## Core Concepts

- Verification combines build, tests, and workflow validation.
- E2E steps validate critical workflows without bypassing guardrails.

---

## How This Evolves Over Time

- Update commands when build/test targets change.
- Update E2E references when new workflows are added or retired.

---

## Common Pitfalls and Anti-Patterns

- Skipping architecture tests or E2E verification when workflows change.
- Treating the checklist as optional for behavioral changes.

---

## When to Change This Document

- The standard build or test commands change.
- A workflow adds or changes required verification steps.

---

## Related Documents

- e2e-email-verification.md
- shared-dev-concurrency.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
