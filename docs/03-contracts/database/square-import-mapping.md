# Square Transactions import: DB mapping (current schema)

Source of truth: `src/DevTools/MGF.SquareImportCli/**`, `src/Data/MGF.Infrastructure/Migrations/*`
Change control: Update when Square import mapping or DB schema changes.
Last verified: 2025-12-30


## Target tables

- `invoices`
- `payments`
- `invoice_integrations_square` (optional 1:1 integration row)

Defined in `src/Data/MGF.Infrastructure/Migrations/20251215075215_Phase1_02_Core.cs`.

## Relevant columns (payments)

- Amount: `payments.amount` (`numeric(12,2)`, NOT NULL)
- Currency: `payments.currency_code` (`text`, NOT NULL, default `"USD"`)
- Status: `payments.status_key` (`text`, NOT NULL, FK `payment_statuses.status_key`)
- Occurred/captured time: `payments.captured_at` (`timestamptz`, NULL) (row timestamp also exists as `payments.created_at`)

## Relevant columns (invoices)

- `invoices.invoice_number` is UNIQUE and must match `^MGF-INV-[0-9]{2}-[0-9]{6}$` (check constraint).
- `invoices.project_id` is NOT NULL (FK to `projects.project_id`).
- `invoices.client_id` is NOT NULL (FK to `clients.client_id`).
- Amounts: `invoices.subtotal_amount`, `invoices.total_amount` (`numeric(12,2)`, NOT NULL), with optional `tax_rate`/`tax_amount`.
- Timestamps: `invoices.issued_at` (NOT NULL), optional `paid_at`.

## Metadata / raw payload

There is no `jsonb` metadata/payload column on `payments` or `invoices` in the current migrations.

## External id fields (candidate natural key)

- `payments.processor_key` (`text`, NULL, FK `payment_processors.processor_key`)
- `payments.processor_payment_id` (`text`, NULL) — indexed via `IX_payments_processor_payment_id` but **not unique**
- `payments.processor_refund_id` (`text`, NULL)

## Square id storage + idempotency (importer)

- Store the Square export’s `Transaction ID` in `payments.processor_payment_id` with `payments.processor_key = 'square'`.
- Enforce idempotency by looking up an existing `payments` row by (`processor_key`, `processor_payment_id`) and reusing its `invoice_id`.
- Create/update `invoice_integrations_square.square_customer_id` when available.

## Notes / constraints

`payments.invoice_id` is NOT NULL (FK to `invoices.invoice_id`), so every payment row must be attached to an invoice.

## Importer details (current behavior)

- `invoices.currency_code` / `payments.currency_code`: uses optional CSV `Currency`/`Currency Code` if present; defaults to `USD`.
- `invoices.tax_amount`: populated from Square `Tax` when parseable; otherwise `0.00`.
- If `square_customer_id` does not resolve to a client, rows are imported under a single system "unmatched" client (marked via a `clients.notes` marker).
- Since `invoices.project_id` is NOT NULL, the importer ensures a per-client ledger project named `Square Transactions (Imported)`.
