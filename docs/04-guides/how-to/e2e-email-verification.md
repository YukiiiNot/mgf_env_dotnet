# E2E Email Verification (Dev)

This procedure verifies delivery email end-to-end without loosening the recipient guardrails. The send path always
uses canonical recipients from the database.

## Prereqs
- Dev configuration loaded (`MGF_ENV`, `MGF_DB_MODE`, `MGF_CONFIG_DIR`).
- Email enabled: `Integrations:Email:Enabled=true`.
- Preview provider (recommended for dev): `Integrations:Email:Provider=preview`.
  - Optional output path: `Integrations:Email:Preview:OutputDir`.
  - Default output path: `runtime/email_preview` under the current working directory.

## Steps
1) Seed a fixture project and canonical recipient:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr>
```
This prints `project_id`, canonical `recipients`, and whether `stableShareUrl` exists. If the stable link is missing,
the command prints the exact delivery command to run.

If you already have a project with a stable share link, pass it explicitly:
```powershell
dotnet run -c Release --project src\DevTools\MGF.ProjectBootstrapDevCli -- seed-e2e-delivery-email --email <addr> --projectId <project_id>
```

2) If required, run the printed delivery command to create the stable share link.

3) Enqueue the delivery email job using the same recipient:
```powershell
dotnet run -c Release --project src\Operations\MGF.ProjectBootstrapCli -- delivery-email `
  --projectId <project_id> `
  --to <addr> `
  --from deliveries@mgfilms.pro `
  --replyTo info@mgfilms.pro
```

4) Process the job:
```powershell
dotnet run -c Release --project src\Services\MGF.Worker -- --maxJobs 1
```

5) Verify:
- `delivery.current.lastEmail.status` updates on the project.
- `delivery.current.lastEmail.provider` is `preview` if the preview provider is selected.
- Preview artifacts exist in the output directory (`message.txt`, `message.html`, `message.json`).

## Notes
- The delivery-email use-case ignores payload overrides; it validates that observed recipients match the canonical
  recipients from the database.
