# Dev Secrets Tool

This tool exports/imports **developer-only** .NET User Secrets based on `tools/dev-secrets/secrets.required.json`. It is **intentionally restrictive**: only the Dev direct DB connection string is allowed; Prod/Staging/CI keys are blocked.

Inventory and rationale: `docs/03-contracts/configuration/dev-secrets.md`.

## Commands

### Export (from your machine)

```powershell
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- export --out .\dev-secrets.export.json
```

Optional:

```powershell
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- export --out .\dev-secrets.export.json --verbose
```

### Import (on a new dev machine)

```powershell
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- import --in .\dev-secrets.export.json
```

Dry run:

```powershell
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- import --in .\dev-secrets.export.json --dry-run
```

### Validate (required keys present)

```powershell
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- validate
```

## Wrapper Scripts

Windows (PowerShell):

```powershell
.\tools\dev-secrets\export.ps1 --out .\dev-secrets.export.json
.\tools\dev-secrets\import.ps1 --in .\dev-secrets.export.json
```

macOS/Linux (bash):

```bash
./tools/dev-secrets/export.sh --out ./dev-secrets.export.json
./tools/dev-secrets/import.sh --in ./dev-secrets.export.json
```

## Security Notes

- **Do not commit** any export files. They are ignored by `.gitignore`.
- Exported files must only be shared securely between developers.
- This tool refuses to export/import any key that looks like Prod/Staging/CI.
- Only the **Dev direct connection string** is allowed for database access.

## Troubleshooting

- `dotnet` missing: install the .NET SDK and ensure `dotnet` is on PATH.
- Missing UserSecretsId: check the project's `.csproj` or `secrets.required.json`.
- Missing required keys: run `devsecrets validate` and set the missing keys.
- Validation errors: ensure your export JSON only includes keys from `secrets.required.json`.
- Disallowed DB key: only `Database:Dev:DirectConnectionString` is accepted.

## Smoke test (fake values)

```powershell
# Set fake values for a quick round-trip test
dotnet user-secrets set "Database:Dev:DirectConnectionString" "Host=localhost;Database=postgres;Username=postgres;Password=fake" --id 8f8e4093-a213-4629-bbd1-2a67c4e9000e

# Export
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- export --out .\dev-secrets.export.json

# Import (dry-run)
dotnet run --project tools/dev-secrets/src/DevSecretsTool -- import --in .\dev-secrets.export.json --dry-run
```
