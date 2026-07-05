param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$configDir = Join-Path $ProjectRoot "docker\\sensor-configs"
$projectPath = Join-Path $ProjectRoot "src\\SensorClient\\SensorClient.csproj"

1..7 | ForEach-Object {
    $configPath = Join-Path $configDir ("sensor-{0:d2}.json" -f $_)
    $command = "Set-Location '$ProjectRoot'; `$env:SENSOR_CONFIG_PATH='$configPath'; dotnet run --project '$projectPath'"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $command
}
