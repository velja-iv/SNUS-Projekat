#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-"$SCRIPT_DIR/../docker/keys"}"
SENSOR_COUNT="${SENSOR_COUNT:-7}"
FORCE="${FORCE:-0}"

mkdir -p "$OUTPUT_DIR"

generate_pair() {
  local base_name="$1"
  local private_path="$OUTPUT_DIR/${base_name}-private.pem"
  local public_path="$OUTPUT_DIR/${base_name}-public.pem"

  if [[ "$FORCE" != "1" && -f "$private_path" && -f "$public_path" ]]; then
    echo "Skipping existing key pair for $base_name"
    return
  fi

  openssl genrsa -out "$private_path" 2048 >/dev/null 2>&1
  openssl rsa -in "$private_path" -pubout -out "$public_path" >/dev/null 2>&1
}

generate_pair "server"

for index in $(seq -w 1 "$SENSOR_COUNT"); do
  generate_pair "sensor-${index}"
done

echo "Key generation completed in $OUTPUT_DIR"
