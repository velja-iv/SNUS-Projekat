param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\\docker\\keys"),
    [int]$SensorCount = 7,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$resolvedOutput = New-Item -ItemType Directory -Force -Path $OutputDir
$openSslCommand = Get-Command openssl -ErrorAction SilentlyContinue

if ($null -eq $openSslCommand) {
    throw "OpenSSL was not found in PATH. Install OpenSSL or add it to PATH before running this script."
}

function Invoke-OpenSsl {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $openSslCommand.Source @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "OpenSSL command failed: openssl $($Arguments -join ' ')"
    }
}

function New-KeyPair {
    param([string]$BaseName)

    $privateKeyPath = Join-Path $resolvedOutput.FullName "$BaseName-private.pem"
    $publicKeyPath = Join-Path $resolvedOutput.FullName "$BaseName-public.pem"

    if (-not $Force -and (Test-Path $privateKeyPath) -and (Test-Path $publicKeyPath)) {
        Write-Host "Skipping existing key pair for $BaseName"
        return
    }

    try {
        Invoke-OpenSsl -Arguments @("genrsa", "-out", $privateKeyPath, "2048")
        Invoke-OpenSsl -Arguments @("rsa", "-in", $privateKeyPath, "-pubout", "-out", $publicKeyPath)
    }
    catch {
        if (Test-Path $privateKeyPath) {
            Remove-Item -LiteralPath $privateKeyPath -Force
        }

        if (Test-Path $publicKeyPath) {
            Remove-Item -LiteralPath $publicKeyPath -Force
        }

        throw
    }
}

New-KeyPair -BaseName "server"
for ($index = 1; $index -le $SensorCount; $index++) {
    New-KeyPair -BaseName ("sensor-{0:d2}" -f $index)
}

Write-Host "Key generation completed in $($resolvedOutput.FullName)"
