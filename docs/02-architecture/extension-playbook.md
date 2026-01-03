# Extension Playbook

**Title:** Extension Playbook  
**Purpose:** Provide a repeatable, low-risk path to add new workflows or concepts without architectural drift.  
**Audience:** Engineers building new features or integrations.  
**Scope:** Workflow/concept extension steps, decision checklist, and examples; not a feature roadmap.  
**Last updated:** 2026-01-02  
**Owner:** Architecture  
**Related docs:** [business-concepts-catalog.md](business-concepts-catalog.md), [domain-persistence-map.md](domain-persistence-map.md), [persistence-patterns.md](persistence-patterns.md), [project-shapes.md](project-shapes.md)

## Step-by-step workflow
1) **Classify the change**  
   Decide if this is a new workflow, a new concept, or an integration.
2) **Update the catalog**  
   Add/update the concept in [business-concepts-catalog.md](business-concepts-catalog.md).
3) **Define Contracts**  
   Add interfaces and DTOs under `src/Core/MGF.Contracts/Abstractions/<Area>/`.
4) **Add UseCase(s)**  
   Create the use-case under `src/Application/MGF.UseCases/UseCases/<Area>/<UseCaseName>/` with interface + models + implementation.
5) **Implement Data access**  
   Use EF repositories by default. If atomic/SQL is required, add a Store under `src/Data/MGF.Data/Stores/<Area>/`.
6) **Wire hosts**  
   API/Worker/Operations should only call UseCases and bind Contracts interfaces.
7) **Tests + docs**  
   Add use-case tests; add SQL-shape tests if raw SQL is involved; update /docs and architecture tests.

## Decision checklist
- Is this a **first-class concept**? If yes, update the catalog.
- Do we need a **Contracts** abstraction (new seam)? If yes, add it before touching hosts.
- Are there reusable **invariants** across workflows? If yes, consider Domain.
- Is raw SQL required? If yes, keep it in Data Stores and document why.
- Is this **vendor-specific**? If yes, keep the client in Integrations; keep orchestration in UseCases.

## Worked example A: minimal Invoices workflow
Goal: create the smallest safe entrypoint for invoices without Domain expansion.
1) Add Contracts interface: `IInvoiceStore` under `MGF.Contracts/Abstractions/Invoices/`.
2) Add UseCase: `UseCases/Finance/Invoices/CreateInvoice/` with request/result models.
3) Implement Data store: `MGF.Data/Stores/Invoices/InvoiceStore.cs` (EF preferred).
4) Wire API or Operations to call the use-case (thin adapter).
5) Add tests: use-case tests + SQL-shape tests if SQL is used.

## Worked example B: new storage provider integration
Goal: add a vendor-specific storage provider without leaking details into UseCases.
1) Add Integrations project: `src/Integrations/MGF.Integrations.<Vendor>/`.
2) Define Contracts gateway: `IStorageProviderClient` (or similar) under `MGF.Contracts/Abstractions/Storage/`.
3) Implement the client in Integrations; keep provider-specific config there.
4) Use a Services adapter if host-specific IO glue is required.
5) UseCases call only Contracts abstractions, never vendor SDKs directly.

## Guardrails checklist (per PR)
- `/docs` updated and linked from [00-index.md](../00-index.md)
- Architecture tests updated if new contracts/shapes are introduced
- UseCases do not reference Data or EF
- Integrations is vendor-only; Platform remains business-agnostic
