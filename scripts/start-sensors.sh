#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$PROJECT_ROOT/src/SensorClient/SensorClient.csproj"

for index in 01 02 03 04 05 06 07; do
  SENSOR_CONFIG_PATH="$PROJECT_ROOT/docker/sensor-configs/sensor-${index}.json" \
    dotnet run --project "$PROJECT_PATH" &
done

wait
