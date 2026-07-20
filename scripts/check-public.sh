#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

tracked_private="$(git ls-files | grep -E '(^|/)private-assets/|(^|/)\.env\.local$' || true)"
if [[ -n "$tracked_private" ]]; then
  echo "Private local files are tracked by git. Remove them before publishing:" >&2
  echo "$tracked_private" >&2
  exit 1
fi

echo "Tracked files:"
git ls-files

echo
echo "Ignored private files:"
git status --short --ignored private-assets .env.local .build windows/**/private-assets windows/**/bin windows/**/obj || true

echo
echo "Public check passed."
