# <Document Title>

> Optional subtitle (one sentence).

**Purpose:** Why this document exists   
**Scope:** What this document covers and does not cover  
**Doc Type:** See doc-enumerations.md  
**Status:** See doc-enumerations.md
**Last Updated:** YYYY-MM-DD (see doc-enumerations.md) 

---

## TL;DR

> Short summary for readers who may not read the full document.

---

## Main Content

> This is the core of the document. Organize it however you want.

> Optional examples (use or ignore):

> How-To: prerequisites -> steps -> verification
> Runbook: symptoms -> diagnosis -> resolution -> rollback
> Reference: definitions -> rules -> invariants
> Decision: context -> options -> decision -> consequences

> You can:

> Add any headings
> Use lists, diagrams, or code blocks
> Remove any sections you do not need

---

## System Context

> Explain **where this topic fits into the broader system**.

> Good questions to answer here:
> Which buckets/projects does this interact with?
> Is this business logic, infrastructure, operational tooling, or presentation?
> How does it relate to other major subsystems?

> This section should assume the system is large and growing.

---

## Core Concepts

> Describe the *concepts*, not the implementation details.

> Focus on:
> Definitions
> Responsibilities
> Invariants or constraints (at a high level)
> Mental models developers should use when reasoning about this area

> Avoid:
> File paths unless absolutely necessary
> Step-by-step operational instructions (those belong in runbooks or how-to guides)

---

## How This Evolves Over Time

> Describe how this part of the system is expected to change as the product grows.

> Examples:
> What kinds of extensions are anticipated?
> What kinds of changes are intentionally discouraged?
> What would be a “smell” that this area is being misused?

> This section is critical for preventing drift.

---

## Common Pitfalls and Anti-Patterns

> List known mistakes or misunderstandings, such as:
> Putting logic in the wrong bucket
> Bypassing contracts or guardrails
> Treating implementation details as stable APIs

> This helps onboard new developers without them learning the hard way.

---

## When to Change This Document

> Explicitly state when this document should be updated.

> Examples:
> A new workflow is added that depends on this area
> A boundary or responsibility changes
> A previously planned concept becomes implemented

> This keeps documentation “alive” instead of fossilized.

---

## Related Documents

> List any relevant document names (NO RELATIVE PATHS, JUST NAMES)

## Change Log

> Date format: *YYYY-MM-DD* (see doc-enumerations.md)
