param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\\docker\\keys"),
    [int]$SensorCount = 7
)

$resolvedOutput = New-Item -ItemType Directory -Force -Path $OutputDir

function New-KeyPair {
    param([string]$BaseName)

    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        [System.IO.File]::WriteAllText((Join-Path $resolvedOutput.FullName "$BaseName-private.pem"), $rsa.ExportRSAPrivateKeyPem())
        [System.IO.File]::WriteAllText((Join-Path $resolvedOutput.FullName "$BaseName-public.pem"), $rsa.ExportRSAPublicKeyPem())
    }
    finally {
        $rsa.Dispose()
    }
}

New-KeyPair -BaseName "server"
for ($index = 1; $index -le $SensorCount; $index++) {
    New-KeyPair -BaseName ("sensor-{0:d2}" -f $index)
}

Write-Host "Generated server and sensor key pairs in $($resolvedOutput.FullName)"
