[CmdletBinding()]
param()

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sqlPath = Join-Path $repoRoot "tools\\reset-dev.sql"

Write-Host ""
Write-Host "DEV ONLY reset helper"
Write-Host "1) Open your Supabase DEV project -> SQL Editor"
Write-Host "2) Paste and run this SQL script:"
Write-Host "   $sqlPath"
Write-Host ""
Write-Host "Then this script will run the migrator against MGF_ENV=Dev."
Write-Host ""

if (-not (Test-Path $sqlPath)) {
  throw "Missing SQL script: $sqlPath"
}

$env:MGF_ENV = "Dev"
$env:MGF_DB_MODE = "direct"

dotnet run --project (Join-Path $repoRoot "src\\MGF.Tools.Migrator\\MGF.Tools.Migrator.csproj")
