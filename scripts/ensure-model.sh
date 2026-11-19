#!/usr/bin/env bash
# Make sure the tiny local model is present and intact.
#  1. models/smollm2-135m-instruct-int4 exists (it is committed to git) and matches its SHA-256 manifest -> use it (offline).
#  2. otherwise -> rebuild it with scripts/get-model-smollm2.sh (needs network once) and verify again.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
NAME="smollm2-135m-instruct-int4"
DIR="$ROOT/models/$NAME"
SUMS="$ROOT/models/$NAME.sha256"

verify() {
  [ -f "$DIR/genai_config.json" ] || return 1
  (cd "$DIR" && sha256sum --quiet -c "$SUMS") >/dev/null 2>&1
}

if verify; then
  echo "OK: $NAME is present and matches $SUMS ($(du -sh "$DIR" | cut -f1)) — no download needed."
  exit 0
fi

echo "Model missing or modified at $DIR — rebuilding with scripts/get-model-smollm2.sh"
rm -rf "$DIR"
"$ROOT/scripts/get-model-smollm2.sh" "$DIR"
if verify; then
  echo "OK: rebuilt $NAME and verified checksums."
else
  echo "WARNING: rebuilt model differs from $SUMS (different builder version?). It will still run; record the new hashes." >&2
fi
