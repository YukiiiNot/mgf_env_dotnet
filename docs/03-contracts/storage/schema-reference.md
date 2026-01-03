# Schema Reference

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

﻿# Storage Schema Reference

Source of truth: `artifacts/schemas/mgf.folderTemplate.schema.json`, `artifacts/schemas/mgf.namingRules.schema.json`
Change control: Update when schema files change.
Last verified: 2025-12-30

## Canonical schemas
- Folder template schema: [../../../artifacts/schemas/mgf.folderTemplate.schema.json](../../../artifacts/schemas/mgf.folderTemplate.schema.json)
- Naming rules schema: [../../../artifacts/schemas/mgf.namingRules.schema.json](../../../artifacts/schemas/mgf.namingRules.schema.json)

---

## Metadata

Last updated: 2026-01-02  
Owner: Platform  
Review cadence: on contract change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
