#!/usr/bin/env bash
set -euo pipefail

REPO_URL=""
INSTALL_DIR="${INSTALL_DIR:-$HOME/MenuPet}"
PET_IMAGE_PATH="${PET_IMAGE_PATH:-}"
SPEECH_IMAGE_PATH="${SPEECH_IMAGE_PATH:-}"
APP_NAME="${APP_NAME:-MenuPet}"
BUNDLE_ID="${BUNDLE_ID:-dev.example.menupet}"
ICON_TEXT="${ICON_TEXT:-}"
SPEECH_TEXT="${SPEECH_TEXT:-hug me...}"
OPEN_AFTER_BUILD=1

usage() {
  cat <<'USAGE'
Usage:
  bash install-from-github.sh --repo https://github.com/OWNER/REPO.git --pet /path/to/pet.png [options]

Options:
  --repo URL         Required. GitHub repository URL.
  --dir PATH         Install source directory. Default: ~/MenuPet
  --pet PATH         Required. PNG image for the desktop pet.
  --speech PATH      Optional. PNG image drawn inside the speech bubble.
  --name NAME        App name. Default: MenuPet
  --bundle-id ID     Bundle identifier. Default: dev.example.menupet
  --icon-text TEXT   Text rendered in the app icon. Default: app name
  --speech-text TEXT Fallback speech text when no speech image is provided.
  --no-open          Build/install but do not open the app.
  -h, --help         Show this help.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)
      REPO_URL="${2:-}"
      shift 2
      ;;
    --dir)
      INSTALL_DIR="${2:-}"
      shift 2
      ;;
    --pet)
      PET_IMAGE_PATH="${2:-}"
      shift 2
      ;;
    --speech)
      SPEECH_IMAGE_PATH="${2:-}"
      shift 2
      ;;
    --name)
      APP_NAME="${2:-}"
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

if [[ -z "$REPO_URL" ]]; then
  echo "Missing required --repo URL." >&2
  usage >&2
  exit 1
fi

if [[ -z "$PET_IMAGE_PATH" ]]; then
  echo "Missing required --pet PATH." >&2
  usage >&2
  exit 1
fi

if ! command -v git >/dev/null 2>&1; then
  echo "git is required. Install Xcode Command Line Tools with: xcode-select --install" >&2
  exit 1
fi

if ! command -v swiftc >/dev/null 2>&1; then
  echo "swiftc is required. Install Xcode Command Line Tools with: xcode-select --install" >&2
  exit 1
fi

if [[ -d "$INSTALL_DIR/.git" ]]; then
  git -C "$INSTALL_DIR" pull --ff-only
else
  mkdir -p "$(dirname "$INSTALL_DIR")"
  git clone "$REPO_URL" "$INSTALL_DIR"
fi

install_args=(
  --pet "$PET_IMAGE_PATH"
  --name "$APP_NAME"
  --bundle-id "$BUNDLE_ID"
  --speech-text "$SPEECH_TEXT"
)

if [[ -n "$SPEECH_IMAGE_PATH" ]]; then
  install_args+=(--speech "$SPEECH_IMAGE_PATH")
fi

if [[ -n "$ICON_TEXT" ]]; then
  install_args+=(--icon-text "$ICON_TEXT")
fi

if [[ "$OPEN_AFTER_BUILD" -eq 0 ]]; then
  install_args+=(--no-open)
fi

"$INSTALL_DIR/scripts/install-local.sh" "${install_args[@]}"
