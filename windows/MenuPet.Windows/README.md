# MenuPet.Windows

Native Windows desktop pet built with .NET 8, WPF, and a Windows system tray icon.

No personal images are included. Choose your own transparent PNG from the tray menu or pass one to the installer.

## Features

- Windows system tray app
- Transparent always-on-top pet window
- PNG alpha support through WPF image rendering
- Show, hide, scale, movement-speed controls
- Random walk, bounce, and rest modes
- Editable speech bubble text
- Draggable chicken breast food
- Eating animation with hearts and `냠냠`
- Manual and random bench press animation
- Cursor throw when the mouse enters the pet

## Requirements

- Windows 10 or Windows 11
- Git for Windows
- .NET 8 SDK

## Install From GitHub

Run PowerShell:

```powershell
iwr -UseBasicParsing https://raw.githubusercontent.com/ddingddong9/MenuPet/main/scripts/install-windows.ps1 -OutFile "$env:TEMP\install-menupet.ps1"
powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-menupet.ps1" -PetPath "C:\path\to\pet.png"
```

Custom name and speech text:

```powershell
powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-menupet.ps1" `
  -PetPath "C:\path\to\pet.png" `
  -Name "Desk Buddy" `
  -SpeechText "hello!"
```

The app is published to:

```text
%USERPROFILE%\Desktop\MenuPet.Windows
```

Private settings and your copied PNG are stored in:

```text
%APPDATA%\MenuPet
```

## Build Manually

```powershell
git clone https://github.com/ddingddong9/MenuPet.git
cd MenuPet
dotnet publish .\windows\MenuPet.Windows\MenuPet.Windows.csproj -c Release -r win-x64 --self-contained false -o "$env:USERPROFILE\Desktop\MenuPet.Windows"
```

Run:

```powershell
& "$env:USERPROFILE\Desktop\MenuPet.Windows\MenuPet.Windows.exe"
```

Stop:

```powershell
Stop-Process -Name MenuPet.Windows
```
