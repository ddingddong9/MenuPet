#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_NAME="MenuPet"
EXECUTABLE_NAME="MenuPet"
BUNDLE_ID="dev.example.menupet"
ICON_TEXT=""
SPEECH_TEXT="hug me..."
PET_INPUT=""
SPEECH_INPUT=""
OPEN_AFTER_BUILD=1

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/install-local.sh --pet /path/to/pet.png [options]

Options:
  --pet PATH          Required. PNG image for the desktop pet.
  --speech PATH      Optional. PNG image drawn inside the speech bubble.
  --name NAME        App name. Default: MenuPet
  --bundle-id ID     Bundle identifier. Default: dev.example.menupet
  --icon-text TEXT   Text rendered in the app icon. Default: app name
  --speech-text TEXT Fallback speech text when no speech image is provided.
  --no-open          Build/install but do not open the app.
  -h, --help         Show this help.

Example:
  ./scripts/install-local.sh --pet ~/Desktop/pet.png --name "My Pet"
USAGE
}

shell_quote() {
  local value="$1"
  printf "'%s'" "$(printf "%s" "$value" | sed "s/'/'\\\\''/g")"
}

absolute_path() {
  local path="$1"
  local dir
  dir="$(cd "$(dirname "$path")" && pwd -P)"
  printf "%s/%s" "$dir" "$(basename "$path")"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pet)
      PET_INPUT="${2:-}"
      shift 2
      ;;
    --speech)
      SPEECH_INPUT="${2:-}"
      shift 2
      ;;
    --name)
      APP_NAME="${2:-}"
      EXECUTABLE_NAME="$(printf "%s" "${2:-}" | tr -cd '[:alnum:]_-' | cut -c 1-48)"
      shift 2
      ;;
    --bundle-id)
      BUNDLE_ID="${2:-}"
      shift 2
      ;;
    --icon-text)
      ICON_TEXT="${2:-}"
      shift 2
      ;;
    --speech-text)
      SPEECH_TEXT="${2:-}"
      shift 2
      ;;
    --no-open)
      OPEN_AFTER_BUILD=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$PET_INPUT" ]]; then
  echo "Missing required --pet PATH." >&2
  usage >&2
  exit 1
fi

if [[ ! -f "$PET_INPUT" ]]; then
  echo "Pet image not found: $PET_INPUT" >&2
  exit 1
fi

if [[ "${PET_INPUT##*.}" != "png" && "${PET_INPUT##*.}" != "PNG" ]]; then
  echo "Pet image must be a PNG file: $PET_INPUT" >&2
  exit 1
fi

if [[ -z "$EXECUTABLE_NAME" ]]; then
  EXECUTABLE_NAME="MenuPet"
fi

if [[ -z "$ICON_TEXT" ]]; then
  ICON_TEXT="$APP_NAME"
fi

mkdir -p "$ROOT_DIR/private-assets"
PET_DEST="$ROOT_DIR/private-assets/pet-character.png"
if [[ "$(absolute_path "$PET_INPUT")" != "$PET_DEST" ]]; then
  cp -p "$PET_INPUT" "$PET_DEST"
fi

if [[ -n "$SPEECH_INPUT" ]]; then
  if [[ ! -f "$SPEECH_INPUT" ]]; then
    echo "Speech image not found: $SPEECH_INPUT" >&2
    exit 1
  fi
  SPEECH_DEST="$ROOT_DIR/private-assets/speech-message.png"
  if [[ "$(absolute_path "$SPEECH_INPUT")" != "$SPEECH_DEST" ]]; then
    cp -p "$SPEECH_INPUT" "$SPEECH_DEST"
  fi
fi

{
  echo "# Local private build config. This file is ignored by git."
  echo "APP_NAME=$(shell_quote "$APP_NAME")"
  echo "EXECUTABLE_NAME=$(shell_quote "$EXECUTABLE_NAME")"
  echo "BUNDLE_ID=$(shell_quote "$BUNDLE_ID")"
  echo "ICON_TEXT=$(shell_quote "$ICON_TEXT")"
  echo "SPEECH_TEXT=$(shell_quote "$SPEECH_TEXT")"
  echo 'PET_IMAGE_PATH="$ROOT_DIR/private-assets/pet-character.png"'
  if [[ -n "$SPEECH_INPUT" ]]; then
    echo 'SPEECH_IMAGE_PATH="$ROOT_DIR/private-assets/speech-message.png"'
  else
    echo 'SPEECH_IMAGE_PATH="$ROOT_DIR/private-assets/speech-message.png"'
  fi
} > "$ROOT_DIR/.env.local"

"$ROOT_DIR/scripts/build-app.sh"

if [[ "$OPEN_AFTER_BUILD" -eq 1 ]]; then
  open "$HOME/Desktop/$APP_NAME.app"
fi

echo
echo "Installed $APP_NAME at $HOME/Desktop/$APP_NAME.app"
echo "Private assets were copied into $ROOT_DIR/private-assets"
