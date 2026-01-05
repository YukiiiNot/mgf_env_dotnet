# Readme

Purpose  
Describe the decision format and how architectural decisions are captured.

Audience  
Engineers authoring or reviewing architectural decisions.

Scope  
Covers the decision format and how to apply it. Does not define runtime behavior.

Status  
Active

---

## Key Takeaways

- This section records architectural decisions and their context.
- Use the ADR template for new decisions.
- Do not treat decisions as runtime behavior.

---

## System Context

Decision records explain why architectural choices were made and how to repeat the process.

---

## Core Concepts

This document records the decision format and how decisions are captured.

---

## How This Evolves Over Time

- Update the template if decision fields change.
- Add guidance when the decision process evolves.

---

## Common Pitfalls and Anti-Patterns

- Skipping the ADR template or missing context fields.
- Treating a decision as a runtime requirement.

---

## When to Change This Document

- Decision fields or process change.
- New guidance is needed for decision capture.

---

## Related Documents

- adr-template.md
- ../02-architecture/system-overview.md
- ../02-architecture/application-layer-conventions.md

---

## Appendix (Optional)

### Prior content (preserved for reference)

# Architecture Decisions (ADR)

Use ADRs to capture decisions that affect architecture, contracts, or operations.

## Format
- One decision per file.
- Use kebab-case file names with a numeric prefix (e.g., `0001-storage-provider-adapter.md`).

## Template
- [adr-template.md](adr-template.md)

---

## Metadata

Last updated: 2026-01-02  
Owner: Architecture  
Review cadence: on major architecture change  

Change log:
- 2026-01-02 - Reformatted to the documentation template.
