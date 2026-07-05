#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-"$SCRIPT_DIR/../docker/keys"}"
SENSOR_COUNT="${SENSOR_COUNT:-7}"

mkdir -p "$OUTPUT_DIR"

openssl genrsa -out "$OUTPUT_DIR/server-private.pem" 2048 >/dev/null 2>&1
openssl rsa -in "$OUTPUT_DIR/server-private.pem" -pubout -out "$OUTPUT_DIR/server-public.pem" >/dev/null 2>&1

for index in $(seq -w 1 "$SENSOR_COUNT"); do
  openssl genrsa -out "$OUTPUT_DIR/sensor-${index}-private.pem" 2048 >/dev/null 2>&1
  openssl rsa -in "$OUTPUT_DIR/sensor-${index}-private.pem" -pubout -out "$OUTPUT_DIR/sensor-${index}-public.pem" >/dev/null 2>&1
done

echo "Generated server and sensor key pairs in $OUTPUT_DIR"
