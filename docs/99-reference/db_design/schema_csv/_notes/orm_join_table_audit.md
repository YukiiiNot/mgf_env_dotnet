# ORM Join Table Audit

Purpose  
Provide stable reference information for this area.

Audience  
Engineers needing canonical reference information.

Scope  
Covers stable reference material. Does not provide step-by-step procedures.

Status  
Active

---

## Key Takeaways

- This page is a reference; it is not a step-by-step guide.
- Treat listed conventions as the canonical baseline.
- Update this doc when conventions change.

---

## System Context

Reference docs capture stable conventions and definitions used across the repo.

---

## Core Concepts

This document records the canonical reference information for the repo.

---

## How This Evolves Over Time

- Update as conventions evolve.
- Remove deprecated guidance once fully superseded.

---

## Common Pitfalls and Anti-Patterns

- Treating reference content as a runtime API.
- Leaving outdated conventions unmarked.

---

## When to Change This Document

- Conventions change or become obsolete.
- New reference material is added or removed.

---

## Related Documents

- ../../../documentation-standards.md
- ../../../naming-rules.md
- ../../../style-guide.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

Generated: 2025-12-14 18:55:31Z

## Detected ORM
- Summary: No ORM configured; EF Core appears planned (placeholder DbContext only).

## Evidence Paths
- Placeholder DbContext: `C:\dev\mgf_env_dotnet\src\Data\MGF.Data\Data\AppDbContext.cs`
- No EF Core packages were found referenced in project files (e.g. `C:\dev\mgf_env_dotnet\src\Data\MGF.Data\MGF.Data.csproj` contains no `PackageReference` entries).

## Join Table Modeling in Code
- No occurrences of the join-table names were found in `mgf_env_dotnet` source, so they do not appear to be modeled as entities (and there is no implicit many-to-many mapping configured).

## Join Table Key Strategy (from schema CSVs)
(This reflects your schema documentation under `_core`, since no ORM mapping was detected in code.)

- `lead_tags`: composite PK (lead_id, tag_key)
- `work_item_tags`: composite PK (work_item_id, tag_key)
- `project_members`: surrogate/single PK (project_member_id)
- `booking_attendees`: composite PK (booking_id, person_id)
- `person_roles`: composite PK (person_id, role_key)
- `person_permissions`: composite PK (person_id, permission_key)
- `role_scope_roles`: composite PK (scope_key, role_key)

## Recommendations (No schema changes made)
- `project_members`: keep the surrogate id approach (it has payload/lifecycle columns like `assigned_at`, `released_at`, `is_active`, plus notes). This is ORM-friendly for updates and for referencing a specific assignment row.
- Pure join tables (`lead_tags`, `work_item_tags`, `person_roles`, `person_permissions`, `role_scope_roles`): composite PKs are fine; if you adopt EF Core later, model them as explicit join entities if you need `created_at`, otherwise consider implicit many-to-many.
- `booking_attendees`: consider a surrogate id only if you expect multiple attendance records per (booking, person) over time, or if other tables need to FK a specific attendee row; otherwise a composite PK (booking_id, person_id) is fine.

---

## Metadata

Last updated: 2026-01-02  
Owner: Data  
Review cadence: semiannually  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
