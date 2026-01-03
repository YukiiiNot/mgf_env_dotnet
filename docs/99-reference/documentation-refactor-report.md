# Documentation Refactor Report

Purpose  
Summarize the template refactor across /docs and capture review needs.

Audience  
Documentation maintainers, architecture owners, and engineers updating docs.

Scope  
Covers template compliance status, review flags, and overlap candidates. Does not change system behavior.

Status  
Active

---

## Key Takeaways

- All docs under `/docs` were refactored into the standard template structure.
- Three docs remain flagged as Needs Review and require validation.
- Several topics overlap and should be consolidated in future cleanup.

---

## System Context

This report documents the template enforcement pass across the documentation tree and supports drift prevention.

---

## Core Concepts

### Bucket-by-bucket checklist

- 00-index: `00-index.md`
- 01-onboarding: `getting-started.md`, `dev-guide.md`, `contributing.md`
- 02-architecture: `system-overview.md`, `application-layer-conventions.md`, `project-shapes.md`, `persistence-patterns.md`, `workflows.md`, `provisioning.md`, `dependencies.md`, `database-phase1.md`, `roadmap.md`, `business-concepts-catalog.md`, `domain-persistence-map.md`, `extension-playbook.md`, `testing-and-verification.md`
- 03-contracts: `api/overview.md`, `configuration/*`, `database/*`, `events/*`, `storage/*`
- 04-guides: `how-to/*`, `troubleshooting/*`
- 05-runbooks: `delivery.md`, `destructive-ops-audit.md`, `migrations-ci.md`, `repo-workflow.md`
- 06-decisions: `README.md`, `adr-template.md`
- 99-reference: `documentation-standards.md`, `doc-impact-matrix.md`, `glossary.md`, `naming-rules.md`, `style-guide.md`, `db_design/*`, `templates/*`

### Needs Review list (status + reason)

- `docs/02-architecture/database-phase1.md`: Phase 1 scope may be outdated after refactors.
- `docs/02-architecture/dependencies.md`: Dependencies may have shifted after refactors.
- `docs/99-reference/db_design/PHASE1_STATUS.md`: Phase status may be outdated.

### Overlap candidates (suggested merge targets)

- `docs/05-runbooks/delivery.md` and `docs/04-guides/troubleshooting/delivery.md`: clarify ownership between runbook steps and troubleshooting guidance.
- `docs/04-guides/how-to/project-bootstrap.md` and `docs/02-architecture/provisioning.md`: keep procedural steps in the guide; keep boundary rules in architecture.
- `docs/03-contracts/database/schema.md` and `docs/99-reference/db_design/schema_csv/README.md`: align schema reference with CSV metadata references.
- `docs/03-contracts/events/jobs.md` and `docs/02-architecture/workflows.md`: keep payload detail in contracts; keep workflow context in architecture.

### Missing docs (suggested stubs, not created)

- DevTools overview: a short index of dev-only CLIs and their purpose.
- Integrations overview: a summary of vendor-specific adapters and where to add new ones.
- Operations CLI catalog: a concise map of operational entrypoints and safe usage.

### Major inconsistencies (high-level)

- No concrete inconsistencies found during this pass beyond the three Needs Review items listed above.

---

## How This Evolves Over Time

- Re-run this report after major refactors or bucket changes.
- Update Needs Review items as they are validated against current code.

---

## Common Pitfalls and Anti-Patterns

- Letting template compliance drift after edits.
- Duplicating guidance across guides and runbooks.

---

## When to Change This Document

- A new docs bucket or major doc set is introduced.
- Overlap candidates are merged or removed.
- Additional Needs Review items are discovered.

---

## Related Documents

- documentation-standards.md
- doc-impact-matrix.md
- ../02-architecture/system-overview.md

---

## Appendix (Optional)

No appendix content.

---

## Metadata

Last updated: 2026-01-02  
Owner: Documentation  
Review cadence: quarterly  

Change log:
- 2026-01-02 - Created report for template refactor.
