#!/usr/bin/env sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
BUNDLE_PATH=${1:-"$SCRIPT_DIR/.."}
python3 "$SCRIPT_DIR/validate_bundle.py" "$BUNDLE_PATH"
