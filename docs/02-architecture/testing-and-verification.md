# Testing and Verification

**Title:** Testing and Verification  
**Purpose:** Define the sanctioned, repeatable E2E verification flow and safety rails.  
**Audience:** Engineers running validation, CI, and DevTools workflows.  
**Scope:** E2E verification process and guardrails; not a troubleshooting guide.  
**Last updated:** 2026-01-02  
**Owner:** Architecture  
**Related docs:** [../04-guides/how-to/e2e-email-verification.md](../04-guides/how-to/e2e-email-verification.md), [../05-runbooks/repo-workflow.md](../05-runbooks/repo-workflow.md), [project-shapes.md](project-shapes.md)

## Sanctioned E2E verification flow
Use the existing UseCases and DevTools to validate end-to-end behavior. Avoid manual DB edits.

1) **Bootstrap**  
   Ensure a project is bootstrapped and storage roots exist via UseCases/Operations.
2) **Delivery**  
   Run delivery to produce stable paths and `stableShareUrl`.
3) **Delivery email**  
   Use canonical recipients; do not override recipients in production flows.
4) **RootIntegrity**  
   Run the job via the standard handler to confirm issues are reported.
5) **API flows**  
   Verify CreateProject and Square webhook ingestion via thin controller adapters.

## Canonical recipients policy
- Delivery-email use-cases derive recipients from canonical data.
- Overrides are rejected in production flows.
- DevTools should provide a sanctioned way to seed canonical recipients for E2E testing.

## Preview email provider (Dev-only)
If available, use the preview provider for safe validation:
- Set `Integrations:Email:Provider=preview` in Dev config.
- Preview must preserve composition output and audit metadata; only transport changes.

## DevTools seed command
If the DevTools seed command exists (e.g., `seed-e2e-delivery-email`), use it to:
- Ensure canonical recipients are present.
- Validate `stableShareUrl` exists (or print the delivery command needed to create it).

## Safety rails
- No destructive DevTools in production environments.
- Architecture tests enforce:
  - `/docs` is canonical
  - UseCases do not reference Data/EF
  - Integrations are vendor-only
  - DevTools are not referenced by production code

## Evidence capture
Record commands and outputs in `/docs/validation/` when an E2E run is part of a formal validation pass.
