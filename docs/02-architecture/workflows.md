# Workflows

Purpose  
Define architecture boundaries and responsibilities for this area.

Audience  
Engineers extending or refactoring system boundaries.

Scope  
Covers boundaries, ownership, and dependency direction. Does not include operational steps.

Status  
Active

---

## Key Takeaways

- This doc defines architecture boundaries for this area.
- Follow the bucket ownership rules and dependency direction.
- Use related docs when extending or refactoring.

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

---

## Appendix (Optional)

### Prior content (preserved for reference)

﻿# Workflow Overview

This is a high-level map of core workflows. Detailed step-by-step instructions live in runbooks and how-to guides.

## Core workflows
- Project bootstrap and provisioning: [../04-guides/how-to/project-bootstrap.md](../04-guides/how-to/project-bootstrap.md)
- Delivery pipeline: [../05-runbooks/delivery.md](../05-runbooks/delivery.md)
- Database migrations: [../04-guides/how-to/db-migrations.md](../04-guides/how-to/db-migrations.md)

## Operational workflow
- Repo and CI flow: [../05-runbooks/repo-workflow.md](../05-runbooks/repo-workflow.md)

---

## Metadata

Last updated: 2026-01-02  
Owner: Architecture  
Review cadence: on major architecture change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
