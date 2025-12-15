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

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    if ([string]::IsNullOrWhiteSpace($DbHost)) {
        $DbHost = Read-Host -Prompt "Supabase session pooler host (e.g. YOUR_PROJECT.pooler.supabase.com)"
    }

    if ([string]::IsNullOrWhiteSpace($Username)) {
        $Username = Read-Host -Prompt "Username (e.g. postgres.YOUR_REF)"
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
        "Ssl Mode=Require;Trust Server Certificate=true"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "No connection string provided."
}

$env:Database__ConnectionString = $ConnectionString
Write-Host "Set Database__ConnectionString for this PowerShell session."
