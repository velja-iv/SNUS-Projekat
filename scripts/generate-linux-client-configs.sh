#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <SERVER_IP_OR_HOSTNAME>"
  exit 1
fi

SERVER_IP="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_DIR="$PROJECT_ROOT/linux-client/configs"
TARGET_DIR="$PROJECT_ROOT/linux-client/runtime-configs"

mkdir -p "$TARGET_DIR"

for source_file in "$SOURCE_DIR"/sensor-*.json; do
  target_file="$TARGET_DIR/$(basename "$source_file")"
  sed "s/__SERVER_IP__/$SERVER_IP/g" "$source_file" > "$target_file"
done

echo "Generated Linux client configs in: $TARGET_DIR"
echo "Server address: $SERVER_IP"
