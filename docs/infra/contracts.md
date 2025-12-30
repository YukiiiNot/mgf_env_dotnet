# Infrastructure Contracts

This document explains the intent behind our infrastructure contracts. It is for humans, not schemas.

## What templates represent

Templates under `docs/templates/` are contracts, not suggestions. They define the expected folder layout
for domains and projects. They must stay stable over time so old projects still match the contract.

## What the Provisioner guarantees

The Provisioner:

- validates templates against schemas before use
- plans and applies in a deterministic, idempotent way
- never deletes files
- writes a manifest (`folder_manifest.json`) under `00_Admin\.mgf\manifest\` describing what it did

## What Bootstrap guarantees

Project Bootstrap:

- defaults to non‑destructive behavior
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
- Bootstrap defaults that keep it non‑destructive

## What may evolve (and how)

Allowed changes are additive and reviewed:

- new templates
- new optional folders or fields
- new provisioning metadata fields

If a change could affect existing projects, it must be documented and reviewed.
