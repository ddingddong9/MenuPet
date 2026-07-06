#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if git ls-files --error-unmatch private-assets >/dev/null 2>&1; then
  echo "private-assets is tracked by git. Remove it before publishing." >&2
  exit 1
fi

if git ls-files --error-unmatch .env.local >/dev/null 2>&1; then
  echo ".env.local is tracked by git. Remove it before publishing." >&2
  exit 1
fi

echo "Tracked files:"
git ls-files

echo
echo "Ignored private files:"
git status --short --ignored private-assets .env.local .build || true

echo
echo "Public check passed."
