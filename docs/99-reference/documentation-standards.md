# <Document Title>

Purpose  
One or two sentences explaining *why this document exists*.  
What problem does it solve for the reader?

Audience  
Who this document is for (e.g., new developers, operators, infra maintainers, contributors, UI devs).  
If multiple audiences apply, list them.

Scope  
What this document **covers** and, just as importantly, what it **does not cover**.  
This prevents scope creep and confusion as the system evolves.

Status  
One of:
- Draft – evolving, may be incomplete or partially accurate
- Active – expected to be accurate and relied upon
- Deprecated – no longer authoritative, kept for historical context

---

## Key Takeaways

A short, skimmable list (3–7 bullets) answering:
- What should the reader walk away understanding?
- What decisions does this document help them make?

Example:
- Where this part of the system fits in the overall architecture
- What kinds of changes are appropriate here
- Where to go for deeper or more specific information

---

## System Context

Explain **where this topic fits into the broader system**.

Good questions to answer here:
- Which buckets/projects does this interact with?
- Is this business logic, infrastructure, operational tooling, or presentation?
- How does it relate to other major subsystems?

This section should assume the system is large and growing.

---

## Core Concepts

Describe the *concepts*, not the implementation details.

Focus on:
- Definitions
- Responsibilities
- Invariants or constraints (at a high level)
- Mental models developers should use when reasoning about this area

Avoid:
- File paths unless absolutely necessary
- Step-by-step operational instructions (those belong in runbooks or how-to guides)

---

## How This Evolves Over Time

Describe how this part of the system is expected to change as the product grows.

Examples:
- What kinds of extensions are anticipated?
- What kinds of changes are intentionally discouraged?
- What would be a “smell” that this area is being misused?

This section is critical for preventing drift.

---

## Common Pitfalls and Anti-Patterns

List known mistakes or misunderstandings, such as:
- Putting logic in the wrong bucket
- Bypassing contracts or guardrails
- Treating implementation details as stable APIs

This helps onboard new developers without them learning the hard way.

---

## When to Change This Document

Explicitly state when this document should be updated.

Examples:
- A new workflow is added that depends on this area
- A boundary or responsibility changes
- A previously planned concept becomes implemented

This keeps documentation “alive” instead of fossilized.

---

## Related Documents

Links to other relevant documentation.  
Use this to build a **web of understanding**, not isolated pages.

Examples:
- Architecture overviews
- Runbooks
- Contracts
- How-to guides
- Other onboarding material

---

## Appendix (Optional)

Use this section for:
- Terminology
- Diagrams (if applicable)
- Historical notes
- Non-critical clarifications

Avoid putting core guidance here.

---

## Metadata

Last updated: YYYY-MM-DD  
Owner: Team / Role (e.g., Infra, Platform, Application)  
Review cadence: (e.g., quarterly, on major architecture change)  

Change log:
- YYYY-MM-DD – Brief description of what changed and why
