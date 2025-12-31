# Configuration Reference

Source of truth: `config/appsettings*.json`, `src/MGF.Api/appsettings*.json`, `src/MGF.Worker/appsettings*.json`, `src/Data/MGF.Infrastructure/Options/Options.cs`
Change control: Update when config keys, defaults, or options bindings change.
Last verified: 2025-12-30

## Scope
- App configuration defaults live under `config/` and per-app `appsettings*.json` files.
- Options bindings live in `src/Data/MGF.Infrastructure/Options/Options.cs`.

## Related docs
- Environment variables: [env-vars.md](env-vars.md)
- Dev secrets policy: [dev-secrets.md](dev-secrets.md)
