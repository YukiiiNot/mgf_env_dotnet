# Reset Dev database (Supabase) — DEV ONLY

This workflow **deletes everything in the `public` schema** (tables, data, functions, views, policies, etc.) and recreates the schema so you can re-apply EF migrations cleanly.

## DEV ONLY guardrails (read this first)

- Do **not** run this against anything except your **Dev** Supabase project.
- This is destructive and irreversible (unless you have backups).
- This repo’s production/reset guardrails do not protect you inside the Supabase SQL editor — **you are responsible** for selecting the correct project.

## Step 1 — Reset `public` in Supabase (manual)

1) Open Supabase Dashboard → your **Dev** project
2) Go to SQL Editor
3) Paste and run the script from:

`tools/reset-dev.sql`

### The SQL (same as `tools/reset-dev.sql`)

```sql
-- DEV ONLY: Reset the *Dev* Supabase database by dropping/recreating the public schema.
begin;

drop schema if exists public cascade;
create schema public;

grant usage on schema public to postgres, anon, authenticated, service_role;
grant all on schema public to postgres, service_role;

alter default privileges in schema public grant all on tables to postgres, service_role;
alter default privileges in schema public grant all on sequences to postgres, service_role;
alter default privileges in schema public grant all on functions to postgres, service_role;

alter default privileges in schema public grant select, insert, update, delete on tables to anon, authenticated;
alter default privileges in schema public grant usage, select on sequences to anon, authenticated;
alter default privileges in schema public grant execute on functions to anon, authenticated;

commit;
```

## Step 2 — Re-apply migrations + seed lookups

From repo root:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"
dotnet run --project src/MGF.Tools.Migrator
```

Or use the helper (still does **not** execute SQL automatically; it only points you to it):

```powershell
.\tools\reset-dev.ps1
```

## Step 3 — Run integration tests (Dev only)

Integration tests may truncate data between test classes. They require an explicit opt-in flag:

```powershell
$env:MGF_ENV = "Dev"
$env:MGF_ALLOW_DESTRUCTIVE = "true"
dotnet test .\MGF.sln
```
