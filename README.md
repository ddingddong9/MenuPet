# MenuPet

MenuPet is a desktop-pet template for macOS and Windows.

It lets anyone turn their own transparent PNG into a small desktop companion that wanders around the screen, bounces, rests, jumps, dashes, and occasionally shows a speech bubble.

No personal images are included in this repository.

## Features

### macOS

- Native Swift + AppKit macOS app
- `LSUIElement` menu bar app, so no Dock icon is required
- SF Symbols `heart.fill` status bar icon
- `NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)`
- Transparent borderless non-activating `NSPanel`
- Show, hide, scale, quit menu items
- Motion modes: random walk, bounce, rest
- Optional speech bubble image
- Local-only private assets through `.env.local` and `private-assets/`

### Windows

- Native .NET 8 WPF app
- Windows system tray icon
- Transparent always-on-top pet window
- Show, hide, scale, movement-speed controls
- Editable speech bubble text
- Draggable chicken breast food
- Eating animation with hearts and `냠냠`
- Manual and random bench press animation
- Cursor throw when the mouse enters the pet
- Private assets stored in `%APPDATA%\MenuPet`

## Requirements

### macOS

- macOS 12 or newer
- Xcode Command Line Tools
- A PNG image for your desktop pet

Install Xcode Command Line Tools if needed:

```sh
xcode-select --install
```

### Windows

- Windows 10 or Windows 11
- Git for Windows
- .NET 8 SDK
- A PNG image for your desktop pet

## Easiest Install With Homebrew

Install the generic app:

```sh
brew tap ddingddong9/tap
brew trust ddingddong9/tap
brew install --cask menupet
open -a MenuPet
```

Then click the heart icon in the macOS menu bar and choose:

```text
펫 이미지 선택...
```

Pick your PNG image. MenuPet stores that image locally in your user Library folder, not in this repository.

Optional:

```text
말풍선 이미지 선택...
```

Uninstall:

```sh
brew uninstall --cask menupet
```

## Windows Install

Run PowerShell:

```powershell
iwr -UseBasicParsing https://raw.githubusercontent.com/ddingddong9/MenuPet/main/scripts/install-windows.ps1 -OutFile "$env:TEMP\install-menupet.ps1"
powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-menupet.ps1" -PetPath "C:\path\to\pet.png"
```

Example with a custom app name:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-menupet.ps1" `
  -PetPath "C:\path\to\pet.png" `
  -Name "Desk Buddy" `
  -SpeechText "hello!"
```

The Windows app is published to:

```text
%USERPROFILE%\Desktop\MenuPet.Windows
```

Run it:

```powershell
& "$env:USERPROFILE\Desktop\MenuPet.Windows\MenuPet.Windows.exe"
```

Stop it:

```powershell
Stop-Process -Name MenuPet.Windows
```

## macOS Quick Start

Use this if you want to build from source instead of installing the cask.

```sh
git clone https://github.com/ddingddong9/MenuPet.git MenuPet
cd MenuPet
chmod +x scripts/*.sh
./scripts/install-local.sh --pet /path/to/your/pet.png
```

The app will be built and copied to:

```sh
~/Desktop/MenuPet.app
```

Run it:

```sh
open ~/Desktop/MenuPet.app
```

Stop it:

```sh
pkill -x MenuPet
```

## macOS One-Line Install

After publishing, users can install directly from Terminal with:

```sh
bash -c "$(curl -fsSL https://raw.githubusercontent.com/ddingddong9/MenuPet/main/scripts/install-from-github.sh)" -- \
  --repo https://github.com/ddingddong9/MenuPet.git \
  --pet /path/to/your/pet.png
```

Example with a custom app name:

```sh
bash -c "$(curl -fsSL https://raw.githubusercontent.com/ddingddong9/MenuPet/main/scripts/install-from-github.sh)" -- \
  --repo https://github.com/ddingddong9/MenuPet.git \
  --pet ~/Desktop/my-pet.png \
  --name "My Pet" \
  --bundle-id "com.example.mypet"
```

## Using A Speech Bubble

You can provide a PNG that is drawn inside the speech bubble:

```sh
./scripts/install-local.sh \
  --pet ~/Desktop/my-pet.png \
  --speech ~/Desktop/speech.png
```

If no speech image is provided, the app uses fallback text:

```sh
./scripts/install-local.sh \
  --pet ~/Desktop/my-pet.png \
  --speech-text "hug me..."
```

## Customizing The App

The easiest way is to pass options to `install-local.sh`:

```sh
./scripts/install-local.sh \
  --pet ~/Desktop/my-pet.png \
  --name "Desk Buddy" \
  --bundle-id "com.example.deskbuddy" \
  --icon-text "Buddy" \
  --speech-text "hello!"
```

This creates a local `.env.local` file and copies your image into `private-assets/`.

Both are ignored by git.

## Manual Private Config

You can also copy the example config:

```sh
cp .env.example .env.local
```

Then edit `.env.local`:

```sh
APP_NAME="MenuPet"
EXECUTABLE_NAME="MenuPet"
BUNDLE_ID="dev.example.menupet"
ICON_TEXT="MenuPet"
SPEECH_TEXT="hug me..."
PET_IMAGE_PATH="$ROOT_DIR/private-assets/pet-character.png"
SPEECH_IMAGE_PATH="$ROOT_DIR/private-assets/speech-message.png"
```

Put your private images here:

```sh
mkdir -p private-assets
cp /path/to/your/pet.png private-assets/pet-character.png
cp /path/to/your/speech.png private-assets/speech-message.png
```

Build:

```sh
./scripts/build-app.sh
```

## Updating

If you installed from a cloned repository:

```sh
cd MenuPet
git pull
./scripts/install-local.sh --pet private-assets/pet-character.png
```

If you used the one-line installer:

```sh
bash -c "$(curl -fsSL https://raw.githubusercontent.com/ddingddong9/MenuPet/main/scripts/install-from-github.sh)" -- \
  --repo https://github.com/ddingddong9/MenuPet.git \
  --pet ~/Desktop/my-pet.png
```

## Uninstalling

Stop the running app:

```sh
pkill -x MenuPet
```

Remove the installed app:

```sh
rm -rf ~/Desktop/MenuPet.app
```

Optionally remove the source folder:

```sh
rm -rf ~/MenuPet
```

## Privacy

This repository is designed so personal names and images stay out of git.

Ignored local files:

- `.env.local`
- `private-assets/`
- `.build/`
- `**/bin/`
- `**/obj/`

When installed with Homebrew, selected images are copied into:

```sh
~/Library/Application Support/dev.example.menupet/
```

On Windows, selected images are copied into:

```text
%APPDATA%\MenuPet
```

Before publishing or pushing changes, run:

```sh
./scripts/check-public.sh
```

You can also check what will be committed:

```sh
git status --short
git ls-files --others --exclude-standard
```

## Publishing Your Fork

Before sharing with other users:

1. Create a GitHub repository.
2. Replace every `ddingddong9/MenuPet` in this README with your real repository path if you publish a fork.
3. Make sure no private images are staged.
4. Commit and push.

```sh
git add .env.example .gitignore Info.plist LICENSE README.md Sources scripts
git commit -m "Create public MenuPet template"
git branch -M main
git remote add origin https://github.com/ddingddong9/MenuPet.git
git push -u origin main
```

## Troubleshooting

If `swiftc` is missing:

```sh
xcode-select --install
```

If the app does not rebuild with your latest image, stop it and reinstall:

```sh
pkill -x MenuPet
./scripts/install-local.sh --pet /path/to/your/pet.png
open ~/Desktop/MenuPet.app
```

If your app name has spaces, use quotes:

```sh
./scripts/install-local.sh --pet ~/Desktop/pet.png --name "My Pet"
```

If macOS blocks opening the app, open it from Finder once or allow it in System Settings > Privacy & Security. The app is built locally and ad-hoc signed by the build script.

## License

MIT
