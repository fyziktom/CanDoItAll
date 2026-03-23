#!/usr/bin/env sh
set -eu

IPFS_PATH="${IPFS_PATH:-/data/ipfs}"

if [ ! -f "$IPFS_PATH/config" ]; then
  ipfs init --profile=server
fi

ipfs bootstrap rm --all || true

if [ -f /bootstrap-peers.txt ]; then
  while IFS= read -r line; do
    if [ -n "$line" ]; then
      ipfs bootstrap add "$line"
    fi
  done < /bootstrap-peers.txt
fi

exec ipfs daemon --migrate=true
