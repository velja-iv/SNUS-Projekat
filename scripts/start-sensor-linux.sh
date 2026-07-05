#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <SENSOR_NUMBER>"
  echo "Example: $0 01"
  exit 1
fi

SENSOR_NUMBER="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIG_PATH="$PROJECT_ROOT/linux-client/runtime-configs/sensor-${SENSOR_NUMBER}.json"
PROJECT_PATH="$PROJECT_ROOT/src/SensorClient/SensorClient.csproj"

if [[ ! -f "$CONFIG_PATH" ]]; then
  echo "Runtime config not found: $CONFIG_PATH"
  echo "Run ./scripts/generate-linux-client-configs.sh <SERVER_IP> first."
  exit 1
fi

cd "$PROJECT_ROOT"
export SENSOR_CONFIG_PATH="$CONFIG_PATH"
dotnet run --project "$PROJECT_PATH"
