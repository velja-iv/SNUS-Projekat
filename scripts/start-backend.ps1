param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Set-Location $projectRoot
powershell -ExecutionPolicy Bypass -File .\scripts\generate-keys.ps1

$composeArgs = @("-f", ".\docker\docker-compose.backend.yml", "up")
if (-not $NoBuild) {
    $composeArgs += "--build"
}

docker compose @composeArgs
