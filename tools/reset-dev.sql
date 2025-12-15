-- DEV ONLY: Reset the *Dev* Supabase database by dropping/recreating the public schema.
-- WARNING:
-- - This deletes ALL tables, views, functions, types, policies, and data in schema "public".
-- - Do NOT run this against any non-Dev environment.
-- - Paste this into the Supabase SQL Editor for your DEV project only.

begin;

drop schema if exists public cascade;
create schema public;

-- Restore basic schema privileges for Supabase roles.
grant usage on schema public to postgres, anon, authenticated, service_role;
grant all on schema public to postgres, service_role;

-- Ensure future objects created in public have reasonable defaults.
alter default privileges in schema public grant all on tables to postgres, service_role;
alter default privileges in schema public grant all on sequences to postgres, service_role;
alter default privileges in schema public grant all on functions to postgres, service_role;

-- Optional: grant access for API roles (RLS still governs actual access).
alter default privileges in schema public grant select, insert, update, delete on tables to anon, authenticated;
alter default privileges in schema public grant usage, select on sequences to anon, authenticated;
alter default privileges in schema public grant execute on functions to anon, authenticated;

commit;
