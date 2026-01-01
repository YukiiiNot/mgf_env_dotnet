# Provisioning Engine (MGF.Provisioning)

MGF.Provisioning is the reusable engine for planning and applying folder templates.
It does not choose templates; hosts and use-cases decide which template and tokens to use.

## Engine vs policy
- Engine: template loading, plan generation, execution, and manifest writing.
- Policy: naming and placement rules that are specific to MGF.

Policy implementations live in `src/Platform/MGF.Provisioning/Provisioning/Policy`.
The default policy is `MgfDefaultProvisioningPolicy` and enforces:
- Top-level folder names must match `^\d{2}_.+`
- `.mgf` is only allowed under `00_Admin`
- Manifest folder lives at `00_Admin/.mgf/manifest`

## Selecting templates
Template selection belongs to use-cases and hosts (Worker/CLI/API). The engine only
accepts a `ProvisioningRequest` and applies the provided template.
