# Infra Contracts

> Contract expectations for provisioning templates, schemas, and storage guarantees.

---

## MetaData

**Purpose:** Document the infrastructure contract boundaries for templates, schemas, and provisioning guarantees.
**Scope:** Covers intent, guarantees, and change control for infrastructure templates. Excludes implementation details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Templates and schemas under `artifacts/` are stable contracts.
- Provisioning and bootstrap are deterministic and non-destructive by default.
- Update this doc when template structure, schema, or provisioning guarantees change.

---

## Main Content

Source of truth: `artifacts/templates/*.json`, `artifacts/schemas/*.schema.json`, `src/Operations/MGF.ProvisionerCli`, `src/Operations/MGF.ProjectBootstrapCli`

This document explains the intent behind infrastructure contracts for humans, not schemas.

## Related contracts

- Container templates: `artifacts/templates/*.json`
- Template schemas: `artifacts/schemas/*.schema.json`
- Containers doc: `containers.md`

## What templates represent

Templates under `artifacts/templates/` are contracts, not suggestions. They define the expected folder layout
for domains and projects. They must stay stable over time so old projects still match the contract.

## What the Provisioner guarantees

The Provisioner:

- validates templates against schemas before use
- plans and applies in a deterministic, idempotent way
- never deletes files
- writes a manifest (`folder_manifest.json`) under `00_Admin\.mgf\manifest\` describing what it did

## What Bootstrap guarantees

Project Bootstrap:

- defaults to non-destructive behavior
- tolerates missing roots (skips unless explicitly allowed to create)
- validates templates before any plan/apply/verify
- records results into project metadata for auditing

## Storage provider placeholders

- `nextcloud` is seeded as a placeholder `storage_providers` entry only.
- It is not used by bootstrap/provisioning yet; integration will require a provider adapter decision and workflow changes.

## What must not change silently

- Template structure or naming rules without schema updates and review
- Token rules (`{PROJECT_CODE}`, `{PROJECT_NAME}`, `{CLIENT_NAME}`, `{EDITOR_INITIALS}`)
- Manifest format and location
- Bootstrap defaults that keep it non-destructive

## What may evolve (and how)

Allowed changes are additive and reviewed:

- new templates
- new optional folders or fields
- new provisioning metadata fields

If a change could affect existing projects, it must be documented and reviewed.

---

## System Context

Infrastructure contracts define the stable storage and provisioning boundaries used by services and tooling.

---

## Core Concepts

- Templates and schemas are contracts that guard storage structure.
- Provisioning is deterministic and non-destructive; manifests record applied changes.

---

## How This Evolves Over Time

- Changes must be additive and reviewed to preserve compatibility.
- New storage providers require explicit adapter decisions.

---

## Common Pitfalls and Anti-Patterns

- Silent template edits without schema or contract updates.
- Adding destructive behavior to bootstrap or provisioning.

---

## When to Change This Document

- Template structure, schema, or provisioning guarantees change.
- Storage provider policy changes.

---

## Related Documents
- containers.md
- project-shapes.md
- provisioning.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
