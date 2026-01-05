# Infra Contracts

Purpose  
Define the contract boundary and expectations for this area.

Audience  
Engineers building or consuming contracts and integrations.

Scope  
Covers contract intent and boundary expectations. Does not describe host wiring.

Status  
Active

---

## Key Takeaways

- This document describes a canonical contract boundary.
- Consumers should rely on Contracts rather than host internals.
- Changes must preserve compatibility or be versioned.

---

## System Context

Contracts define stable boundaries between UseCases, Services, and Data.

---

## Core Concepts

This document describes the contract intent and expected usage. Implementation details belong in code.

---

## How This Evolves Over Time

- Update when schema or interface changes are introduced.
- Note compatibility expectations when fields evolve.

---

## Common Pitfalls and Anti-Patterns

- Changing contract shapes without versioning.
- Embedding host-specific types into Contracts.

---

## When to Change This Document

- The contract or schema changes.
- New consumers depend on this boundary.

---

## Related Documents

- ../../02-architecture/system-overview.md
- ../../02-architecture/application-layer-conventions.md
- ../api/overview.md
- ../database/schema.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

# Infrastructure Contracts

Source of truth: `artifacts/templates/*.json`, `artifacts/schemas/*.schema.json`, `src/Operations/MGF.ProvisionerCli`, `src/Operations/MGF.ProjectBootstrapCli`
Change control: Update when template structure, schemas, or provisioning guarantees change.
Last verified: 2025-12-30


This document explains the intent behind our infrastructure contracts. It is for humans, not schemas.

## Related contracts

- Templates: `templates.md`
- Containers: `containers.md`
- Schema reference: `schema-reference.md`

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

- Template structure or naming rules without schema updates + review
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

## Metadata

Last updated: 2026-01-02  
Owner: Platform  
Review cadence: on contract change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
