[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ConnectionString,

    [Parameter(Mandatory = $false)]
    [string]$DbHost,

    [Parameter(Mandatory = $false)]
    [int]$Port = 5432,

    [Parameter(Mandatory = $false)]
    [string]$Database = "postgres",

    [Parameter(Mandatory = $false)]
    [string]$Username,

    [Parameter(Mandatory = $false)]
    [SecureString]$Password
)

$mode = $env:MGF_DB_MODE
if ([string]::IsNullOrWhiteSpace($mode)) {
    $mode = "pooler"
}
$mode = $mode.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    if ([string]::IsNullOrWhiteSpace($DbHost)) {
        if ($mode -eq "direct") {
            $DbHost = Read-Host -Prompt "Supabase direct DB host (e.g. db.YOUR_REF.supabase.co)"
        }
        else {
            $DbHost = Read-Host -Prompt "Supabase session pooler host (e.g. YOUR_PROJECT.pooler.supabase.com)"
        }
    }

    if ([string]::IsNullOrWhiteSpace($Username)) {
        if ($mode -eq "direct") {
            $Username = Read-Host -Prompt "Username (e.g. postgres)"
        }
        else {
            $Username = Read-Host -Prompt "Username (e.g. postgres.YOUR_REF)"
        }
    }

    if ($null -eq $Password) {
        $Password = Read-Host -AsSecureString -Prompt "Password"
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    try {
        $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    $ConnectionString =
        "Host=$DbHost;Port=$Port;Database=$Database;Username=$Username;Password=$passwordPlain;" +
        "Ssl Mode=Require"

    if ($mode -eq "direct") {
        $ConnectionString = $ConnectionString + ";Pooling=false"
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "No connection string provided."
}

$env:Database__Dev__ConnectionString = $ConnectionString
$env:Database__ConnectionString = $ConnectionString

if ($mode -eq "direct") {
    $env:Database__Dev__DirectConnectionString = $ConnectionString
}
else {
    $env:Database__Dev__PoolerConnectionString = $ConnectionString
}

Write-Host "Set Database__Dev__* (mode=$mode) for this PowerShell session."
