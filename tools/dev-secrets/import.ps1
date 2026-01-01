param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repoRoot "tools\dev-secrets\src\MGF.DevSecretsCli\MGF.DevSecretsCli.csproj"
$required = Join-Path $repoRoot "tools\dev-secrets\secrets.required.json"

if (-not (Test-Path $project)) {
    throw "MGF.DevSecretsCli project not found at $project"
}

Push-Location $repoRoot
try {
    dotnet run --project $project -- import --required $required @Args
}
finally {
    Pop-Location
}

