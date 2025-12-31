# Integrations Configuration Contract

Source of truth: `src/Services/MGF.Worker/Integrations/**`, `src/MGF.Api/Square/**`, `config/appsettings*.json`, `tools/dev-secrets/secrets.required.json`
Change control: Update when integration config keys or behavior changes.
Last verified: 2025-12-30

## Scope
- Dropbox, Email, and Square integration settings are configured via appsettings, env vars, and user-secrets.
- Refer to the dev secrets inventory for allowed keys.

## Related docs
- Dev secrets policy: [dev-secrets.md](dev-secrets.md)
