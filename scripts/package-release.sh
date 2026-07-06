#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-1.0.0}"
APP_NAME="MenuPet"
RELEASE_DIR="$ROOT_DIR/.build/release"
ZIP_PATH="$RELEASE_DIR/$APP_NAME-$VERSION.zip"

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

SKIP_ENV_LOCAL=1 \
APP_NAME="$APP_NAME" \
EXECUTABLE_NAME="$APP_NAME" \
BUNDLE_ID="dev.example.menupet" \
ICON_TEXT="$APP_NAME" \
PET_IMAGE_PATH="$RELEASE_DIR/no-private-pet.png" \
SPEECH_IMAGE_PATH="$RELEASE_DIR/no-private-speech.png" \
DESKTOP_DIR="$RELEASE_DIR/install" \
"$ROOT_DIR/scripts/build-app.sh"

COPYFILE_DISABLE=1 ditto -c -k --norsrc --keepParent "$ROOT_DIR/.build/$APP_NAME.app" "$ZIP_PATH"

echo "Release zip: $ZIP_PATH"
echo "SHA-256: $(shasum -a 256 "$ZIP_PATH" | awk '{print $1}')"
