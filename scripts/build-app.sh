#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ "${SKIP_ENV_LOCAL:-0}" != "1" && -f "$ROOT_DIR/.env.local" ]]; then
  set -a
  # shellcheck source=/dev/null
  source "$ROOT_DIR/.env.local"
  set +a
fi

APP_NAME="${APP_NAME:-MenuPet}"
EXECUTABLE_NAME="${EXECUTABLE_NAME:-MenuPet}"
BUNDLE_ID="${BUNDLE_ID:-dev.example.menupet}"
ICON_TEXT="${ICON_TEXT:-$APP_NAME}"
SPEECH_TEXT="${SPEECH_TEXT:-hug me...}"
PET_IMAGE_PATH="${PET_IMAGE_PATH:-$ROOT_DIR/private-assets/pet-character.png}"
SPEECH_IMAGE_PATH="${SPEECH_IMAGE_PATH:-$ROOT_DIR/private-assets/speech-message.png}"
RESOURCE_NAME="pet-character.png"
BUBBLE_MESSAGE_NAME="speech-message.png"
BUILD_DIR="$ROOT_DIR/.build"
APP_PATH="$BUILD_DIR/$APP_NAME.app"
DESKTOP_DIR="${DESKTOP_DIR:-$HOME/Desktop}"
DESKTOP_APP="$DESKTOP_DIR/$APP_NAME.app"
ICONSET_DIR="$BUILD_DIR/AppIcon.iconset"

if [[ ! -f "$PET_IMAGE_PATH" ]]; then
  echo "Pet image not found: $PET_IMAGE_PATH" >&2
  echo "Building a generic app. Users can choose a PNG from the menu after launch." >&2
fi

rm -rf "$APP_PATH"
mkdir -p "$APP_PATH/Contents/MacOS" "$APP_PATH/Contents/Resources"

swiftc \
  -O \
  -framework Cocoa \
  "$ROOT_DIR/Sources/MenuPet/main.swift" \
  -o "$APP_PATH/Contents/MacOS/$EXECUTABLE_NAME"

cp "$ROOT_DIR/Info.plist" "$APP_PATH/Contents/Info.plist"
if [[ -f "$PET_IMAGE_PATH" ]]; then
  cp -p "$PET_IMAGE_PATH" "$APP_PATH/Contents/Resources/$RESOURCE_NAME"
fi
if [[ -f "$SPEECH_IMAGE_PATH" ]]; then
  cp -p "$SPEECH_IMAGE_PATH" "$APP_PATH/Contents/Resources/$BUBBLE_MESSAGE_NAME"
fi

/usr/libexec/PlistBuddy -c "Set :CFBundleDisplayName $APP_NAME" "$APP_PATH/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleExecutable $EXECUTABLE_NAME" "$APP_PATH/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_ID" "$APP_PATH/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleName $APP_NAME" "$APP_PATH/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :PetSpeechText $SPEECH_TEXT" "$APP_PATH/Contents/Info.plist"

"$ROOT_DIR/scripts/create-app-icon.swift" "$ICONSET_DIR" "$ICON_TEXT"
iconutil -c icns "$ICONSET_DIR" -o "$APP_PATH/Contents/Resources/AppIcon.icns"
plutil -lint "$APP_PATH/Contents/Info.plist" >/dev/null

if [[ -f "$PET_IMAGE_PATH" ]]; then
  original_hash="$(shasum -a 256 "$PET_IMAGE_PATH" | awk '{print $1}')"
  bundle_hash="$(shasum -a 256 "$APP_PATH/Contents/Resources/$RESOURCE_NAME" | awk '{print $1}')"

  if [[ "$original_hash" != "$bundle_hash" ]]; then
    echo "PNG SHA-256 mismatch" >&2
    echo "Original: $original_hash" >&2
    echo "Bundle:   $bundle_hash" >&2
    exit 1
  fi
fi

if command -v codesign >/dev/null 2>&1; then
  codesign --force --sign - "$APP_PATH" >/dev/null
fi

rm -rf "$DESKTOP_APP"
ditto "$APP_PATH" "$DESKTOP_APP"

echo "Built: $APP_PATH"
echo "Installed: $DESKTOP_APP"
if [[ -f "$PET_IMAGE_PATH" ]]; then
  echo "Pet PNG SHA-256: $bundle_hash"
else
  echo "Pet PNG SHA-256: not bundled"
fi
