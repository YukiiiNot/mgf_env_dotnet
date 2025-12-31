# System Overview

This repo hosts MGF's internal API, worker, desktop app, and supporting tools for storage provisioning, migrations, and delivery workflows.

## Components
- `src/Services/MGF.Api` - internal API entrypoint for apps and integrations.
- `src/Services/MGF.Worker` - background job processor for provisioning, delivery, and integrations.
- `src/Ui/MGF.Desktop.Wpf` - desktop ops console (early stage).
- `src/MGF.Tools.*` - CLIs for migrations, provisioning, delivery, and audits.
- `src/Data/MGF.Data` - shared data access, configuration, and EF model.

## Related docs
- Workflow overview: [workflows.md](workflows.md)
- Runbooks: [../05-runbooks/repo-workflow.md](../05-runbooks/repo-workflow.md)
- Contracts index: [../03-contracts/](../03-contracts/)
