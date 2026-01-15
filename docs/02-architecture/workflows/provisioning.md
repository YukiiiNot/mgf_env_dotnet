# Provisioning Engine (MGF.FolderProvisioning)

---

## MetaData

**Purpose:** Define architecture boundaries and responsibilities for this area.
**Scope:** Covers boundaries, ownership, and dependency direction. Does not include operational steps.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-06
**Review Cadence:** on major architecture change

---

## TL;DR

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

---

## Main Content

MGF.FolderProvisioning is the folder topology provisioning engine for storage providers (Dropbox/NAS/LucidLink).
It is the reusable engine for planning and applying folder templates.
It does not choose templates; hosts and use-cases decide which template and tokens to use.

### Engine vs policy
- Engine: template loading, plan generation, execution, and manifest writing.
- Policy: naming and placement rules that are specific to MGF.

Policy implementations live in `src/Platform/MGF.FolderProvisioning/Provisioning/Policy`.
The default policy is `MgfDefaultProvisioningPolicy` and enforces:
- Top-level folder names must match `^\d{2}_.+`
- `.mgf` is only allowed under `00_Admin`
- Manifest folder lives at `00_Admin/.mgf/manifest`

### Selecting templates
Template selection belongs to use-cases and hosts (Worker/CLI/API). The engine only
accepts a `ProvisioningRequest` and applies the provided template.

---

## System Context

Architecture docs define bucket responsibilities and dependency direction.

---

## Core Concepts

This document explains the boundary and responsibilities for this area and how it fits into the bucket model.

---

## How This Evolves Over Time

- Update when bucket boundaries or dependency rules change.
- Add notes when a new project or workflow is introduced.

---

## Common Pitfalls and Anti-Patterns

- Putting workflow logic in hosts instead of UseCases.
- Introducing vendor logic outside Integrations.

---

## When to Change This Document

- Bucket ownership or dependency rules change.
- A new workflow impacts the described boundaries.

---

## Related Documents

- system-overview.md
- application-layer-conventions.md
- project-shapes.md
- persistence-patterns.md

## Change Log
- Date format: YYYY-MM-DD (see doc-enumerations.md)
- 2026-01-06 - Reformatted into the new documentation standards format; content preserved.
- 2026-01-02 - Reformatted to the documentation template.
