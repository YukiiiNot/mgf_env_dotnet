# Delivery Runbook (Dev/Test)

Source of truth: `src/MGF.Tools.ProjectBootstrap`, `src/Services/MGF.Worker`, `src/Services/MGF.Worker/Email`
Change control: Update when delivery CLI flags, job flow, or email behavior changes.
Last verified: 2025-12-30


Use this runbook to verify the delivery pipeline end-to-end in Dev without touching production.

## Prereqs

Set environment/config for Dev:

```powershell
$env:MGF_ENV='Dev'
$env:MGF_DB_MODE='direct'
$env:MGF_CONFIG_DIR='C:\dev\mgf_env_dotnet\config'
```

Required secrets/config (set via user-secrets or env vars; do NOT commit secrets):

**Dropbox**

- Integrations:Dropbox:AccessToken **or** Integrations:Dropbox:RefreshToken (+ AppKey/AppSecret)
- Integrations:Dropbox:UseApiRootFolder = true (bot-root mode)
- Integrations:Dropbox:ApiRootFolder = MGFILMS.DELIVERIES

**Email (SMTP relay)**

- Integrations:Email:Enabled = true
- Integrations:Email:Provider = smtp
- Integrations:Email:Smtp:Host = smtp-relay.gmail.com
- Integrations:Email:Smtp:Port = 587
- Integrations:Email:Smtp:UseSsl = true
- From allowlist includes deliveries@mgfilms.pro and info@mgfilms.pro

## Golden Path (copy/paste)

Pick a test project (must be `data_profile=real`). Use a known test project ID.

### 1) Mark ready_to_deliver (if needed)

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- to-deliver --projectId <PROJECT_ID>
```

### 2) Seed a deliverable into LucidLink Final_Masters

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- seed-deliverables `
  --projectId <PROJECT_ID> `
  --file "C:\Users\dorme\Dropbox\MGFILMS.NET\06_DevTest\dropbox_root\99_Dump\deliverable_v1.mp4" `
  --target final-masters `
  --testMode
```

### 3) Enqueue delivery (creates v1 + share link)

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- deliver `
  --projectId <PROJECT_ID> `
  --editorInitials TE `
  --testMode true `
  --allowTestCleanup false `
  --refreshShareLink
```

Process exactly one job:

```powershell
dotnet run -c Release --project src\Services\MGF.Worker --no-build -- --maxJobs 1
```

### 4) Re-run delivery (should reuse share link + no v2)

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- deliver `
  --projectId <PROJECT_ID> `
  --editorInitials TE `
  --testMode true `
  --allowTestCleanup false
dotnet run -c Release --project src\Services\MGF.Worker --no-build -- --maxJobs 1
```

### 5) Send delivery email

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- delivery-email `
  --projectId <PROJECT_ID> `
  --to "info@mgfilms.pro" `
  --from "deliveries@mgfilms.pro" `
  --replyTo "info@mgfilms.pro"
dotnet run -c Release --project src\Services\MGF.Worker --no-build -- --maxJobs 1
```

### 6) Verify

```powershell
dotnet run --project src\MGF.Tools.ProjectBootstrap -- show --projectId <PROJECT_ID>
```

## Expected outputs

**In `show` output**

- `delivery.current.stableShareUrl` present (Dropbox link)
- `delivery.current.shareStatus` = `created` on first run, `reused` on second
- `delivery.current.stablePath` ends with `\01_Deliverables\Final`
- `delivery.current.currentVersion` = `v1`
- `delivery.runs[].email.status` = `sent` (or `failed` with reason)

**Filesystem**

- Dropbox delivery container contains:
  `...\01_Deliverables\Final\v1\deliverable_v1.mp4`
- Manifest at:
  `00_Admin\.mgf\manifest\delivery_manifest.json`

## Troubleshooting (match logs)

**Dropbox token/auth**

Log: `MGF.Worker: Dropbox auth mode=refresh_token source=...`
- If missing: ensure Integrations__Dropbox__RefreshToken or AccessToken is set in env/user-secrets.

**Share link path**

Log: `MGF.Worker: Dropbox share link path=/MGFILMS.DELIVERIES/.../01_Deliverables/Final`
- If missing/incorrect: check ApiRootFolder + UseApiRootFolder.

**Share link failures**

Log: `Dropbox token validation failed` or `Dropbox API error 409`
- Check token validity + share path exists in bot root.

**Email disabled**

Log: `Email sending disabled (Integrations:Email:Enabled=false)`
- Set Integrations__Email__Enabled=true.

**SMTP missing**

Log: `SMTP host not configured (Integrations:Email:Smtp:Host)`
- Set Integrations__Email__Smtp__Host.

**Preview output**

`email-preview` writes `preview.html`, `preview.txt`, `preview.json` into the `--out` folder.
