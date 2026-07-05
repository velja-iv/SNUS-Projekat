param(
    [string]$SourceDir = (Join-Path $PSScriptRoot "..\\docker\\keys"),
    [string]$TargetDir = (Join-Path $PSScriptRoot "..\\linux-client\\keys")
)

$ErrorActionPreference = "Stop"

$resolvedSource = Resolve-Path $SourceDir
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$filesToCopy = @(
    "server-public.pem",
    "sensor-01-private.pem", "sensor-01-public.pem",
    "sensor-02-private.pem", "sensor-02-public.pem",
    "sensor-03-private.pem", "sensor-03-public.pem",
    "sensor-04-private.pem", "sensor-04-public.pem",
    "sensor-05-private.pem", "sensor-05-public.pem",
    "sensor-06-private.pem", "sensor-06-public.pem",
    "sensor-07-private.pem", "sensor-07-public.pem"
)

foreach ($file in $filesToCopy) {
    Copy-Item -LiteralPath (Join-Path $resolvedSource $file) -Destination (Join-Path $TargetDir $file) -Force
}

Write-Host "Copied Linux client PEM files to $TargetDir"
