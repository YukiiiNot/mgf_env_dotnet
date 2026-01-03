
````md
# Getting Started

Purpose  
Get a new developer productive locally without accidentally affecting production workflows, and teach them where to find the “source of truth” for architecture, runbooks, and contracts.

Audience  
New developers onboarding to MGF (including contributors who prefer UI over CLI).

Scope  
Covers local setup, repo orientation, and safe first steps. Does not replace deep runbooks, nor does it teach every workflow in full detail.

Key takeaways
- Start by understanding the bucket model and where code belongs.
- Use DevSecrets to bootstrap local secrets (don’t hand-type lots of user-secrets).
- Run safe verification workflows (including email preview) before changing logic.
- For “how to operate,” use runbooks; for “how to design,” use architecture docs.

## Repo layout (high level)

The system is organized by buckets. Learn the bucket model first, then learn the projects inside each bucket.

Bucket map (canonical):  
- ../02-architecture/project-shapes.md

Common projects you’ll touch early:
- API host: src/Services/MGF.Api
- Worker host: src/Services/MGF.Worker (dispatch + wiring)
- UseCases: src/Application/MGF.UseCases (workflow orchestration boundary)
- Contracts: src/Core/MGF.Contracts (interfaces/shared models)
- Data: src/Data/MGF.Data (EF + stores + migrations)
- Folder provisioning engine: src/Platform/MGF.FolderProvisioning
- Email composition: src/Platform/MGF.Email
- Vendor providers: src/Integrations/MGF.Integrations.*
- Operations CLI: src/Operations/* (operator tools that call UseCases)
- Docs: docs/ (canonical documentation)
- Tests: tests/

## Local setup (safe defaults)

1) Set environment markers for local development
Example (PowerShell):

```powershell
$env:MGF_ENV = 'Dev'
$env:MGF_DB_MODE = 'direct'
$env:MGF_CONFIG_DIR = 'C:\dev\mgf_env_dotnet\config'
````

2. Bootstrap secrets using DevSecrets (preferred)
   DevSecrets exists so new devs don’t manually type many secrets and risk drift.

Read and follow:

* ../04-guides/how-to/dev-secrets-tool.md  (or wherever it lives in your docs tree)

3. Configuration precedence (overview)
   Configuration is built by AddMgfConfiguration. The full precedence rules are documented here:

* ../03-contracts/configuration/ (configuration contracts)
* (Optional) link to the exact code path if you want: src/Data/MGF.Data/Configuration/MgfConfiguration.cs

## First verification steps (before you change code)

Run the baseline checks:

* dotnet build MGF.sln -c Release
* dotnet test MGF.sln -c Release --filter FullyQualifiedName!~MGF.Data.IntegrationTests

Then verify key workflows using the sanctioned verification process:

* ../02-architecture/testing-and-verification.md
* ../05-runbooks/ (pick the relevant workflow runbook)

If you want a UI-first workflow view (for visual onboarding), start with a “dev console” UI that:

* calls API/Operations surfaces safely
* shows job queue state and last workflow status
* never bypasses UseCases

(Implementation guidance for the dev console belongs in UI docs, not here.)

## Core system concepts to learn early

* UseCases are the orchestration boundary: workflows go here.
* Contracts are the handshake: shared models/interfaces live here.
* Domain is intentionally small: it does not mirror every DB table.
* Data owns persistence semantics: EF + SQL stores live here.
* Services and Operations are adapters: thin entry points, not business logic.

For the detailed map from database tables → category → representation:

* ../02-architecture/domain-persistence-map.md
* ../02-architecture/business-concepts-catalog.md

## Where to go next

* Architectural overview and extension guidance:

  * ../02-architecture/system-overview.md
  * ../02-architecture/extension-playbook.md
* Operational runbooks:

  * ../05-runbooks/
* Contracts and templates:

  * ../03-contracts/

Related docs

* ../01-onboarding/contributing.md
* ../01-onboarding/dev-guide.md
* ../02-architecture/project-shapes.md
* ../04-guides/how-to/dev-secrets-tool.md
* ../02-architecture/testing-and-verification.md

Last updated: 2026-01-02
Owner: Repo maintainers / Infra owner
Status: Draft

```

