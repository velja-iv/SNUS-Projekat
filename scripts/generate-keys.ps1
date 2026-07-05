param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\\docker\\keys"),
    [int]$SensorCount = 7,
    [switch]$Force
)

$resolvedOutput = New-Item -ItemType Directory -Force -Path $OutputDir

function New-KeyPair {
    param([string]$BaseName)

    $privateKeyPath = Join-Path $resolvedOutput.FullName "$BaseName-private.pem"
    $publicKeyPath = Join-Path $resolvedOutput.FullName "$BaseName-public.pem"

    if (-not $Force -and (Test-Path $privateKeyPath) -and (Test-Path $publicKeyPath)) {
        Write-Host "Skipping existing key pair for $BaseName"
        return
    }

    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        [System.IO.File]::WriteAllText($privateKeyPath, $rsa.ExportRSAPrivateKeyPem())
        [System.IO.File]::WriteAllText($publicKeyPath, $rsa.ExportRSAPublicKeyPem())
    }
    finally {
        $rsa.Dispose()
    }
}

New-KeyPair -BaseName "server"
for ($index = 1; $index -le $SensorCount; $index++) {
    New-KeyPair -BaseName ("sensor-{0:d2}" -f $index)
}

Write-Host "Key generation completed in $($resolvedOutput.FullName)"
