# Square Transactions Import Mapping (Current Schema)

> Contract mapping between Square exports and the current database schema.

---

## MetaData

**Purpose:** Document how Square import data maps into the current database schema.
**Scope:** Covers target tables, column mappings, and idempotency rules. Excludes importer implementation details.
**Doc Type:** Reference
**Status:** Active
**Last Updated:** 2026-01-07

---

## TL;DR

- Square transactions import into `invoices`, `payments`, and `invoice_integrations_square`.
- Idempotency is enforced via `payments.processor_key` and `payments.processor_payment_id`.
- Update this doc when schema or mapping rules change.

---

## Main Content

Source of truth: `src/DevTools/MGF.SquareImportCli/**`, `src/Data/MGF.Data/Migrations/*`

## Target tables

- `invoices`
- `payments`
- `invoice_integrations_square` (optional 1:1 integration row)

Defined in `src/Data/MGF.Data/Migrations/20251215075215_Phase1_02_Core.cs`.

## Relevant columns (payments)

- Amount: `payments.amount` (`numeric(12,2)`, NOT NULL)
- Currency: `payments.currency_code` (`text`, NOT NULL, default `"USD"`)
- Status: `payments.status_key` (`text`, NOT NULL, FK `payment_statuses.status_key`)
- Occurred/captured time: `payments.captured_at` (`timestamptz`, NULL) (row timestamp also exists as `payments.created_at`)

## Relevant columns (invoices)

- `invoices.invoice_number` is UNIQUE and must match `^MGF-INV-[0-9]{2}-[0-9]{6}$` (check constraint).
- `invoices.project_id` is NOT NULL (FK to `projects.project_id`).
- `invoices.client_id` is NOT NULL (FK to `clients.client_id`).
- Amounts: `invoices.subtotal_amount`, `invoices.total_amount` (`numeric(12,2)`, NOT NULL), with optional `tax_rate` and `tax_amount`.
- Timestamps: `invoices.issued_at` (NOT NULL), optional `paid_at`.

## Metadata and raw payload

There is no `jsonb` metadata or payload column on `payments` or `invoices` in the current migrations.

## External id fields (candidate natural key)

- `payments.processor_key` (`text`, NULL, FK `payment_processors.processor_key`)
- `payments.processor_payment_id` (`text`, NULL) -- indexed via `IX_payments_processor_payment_id` but not unique
- `payments.processor_refund_id` (`text`, NULL)

## Square id storage and idempotency (importer)

- Store the Square export `Transaction ID` in `payments.processor_payment_id` with `payments.processor_key = 'square'`.
- Enforce idempotency by looking up an existing `payments` row by (`processor_key`, `processor_payment_id`) and reusing its `invoice_id`.
- Create or update `invoice_integrations_square.square_customer_id` when available.

## Notes and constraints

`payments.invoice_id` is NOT NULL (FK to `invoices.invoice_id`), so every payment row must be attached to an invoice.

## Importer details (current behavior)

- `invoices.currency_code` and `payments.currency_code`: uses optional CSV `Currency` or `Currency Code` if present; defaults to `USD`.
- `invoices.tax_amount`: populated from Square `Tax` when parseable; otherwise `0.00`.
- If `square_customer_id` does not resolve to a client, rows are imported under a single system "unmatched" client (marked via a `clients.notes` marker).
- Since `invoices.project_id` is NOT NULL, the importer ensures a per-client ledger project named `Square Transactions (Imported)`.

---

## System Context

Square import mapping is a contract between integration ingestion tools and the database schema.

---

## Core Concepts

- Mapping rules ensure imported transactions fit required relational constraints.
- Idempotency relies on processor identifiers rather than raw payload storage.

---

## How This Evolves Over Time

- Update when schema fields, constraints, or importer mapping rules change.
- Record new idempotency rules or external id fields when added.

---

## Common Pitfalls and Anti-Patterns

- Storing raw payload data in new tables without updating this contract.
- Ignoring idempotency and creating duplicate payment rows.

---

## When to Change This Document

- Database schema or import mapping rules change.

---

## Related Documents
- integrations.md
- jobs.md
- repo-workflow.md

## Change Log
- 2026-01-07 - Reformatted to documentation standards.
