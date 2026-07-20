param(
    [string]$Repo = "https://github.com/ddingddong9/MenuPet.git",
    [string]$InstallDir = "$env:USERPROFILE\MenuPet",
    [string]$PetPath = "",
    [string]$Name = "MenuPet",
    [string]$SpeechText = "hug me...",
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

function Require-Command {
    param(
        [string]$Name,
        [string]$InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required. $InstallHint"
    }
}

function Resolve-ExistingFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "File not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

Require-Command "git" "Install Git for Windows: https://git-scm.com/download/win"
Require-Command "dotnet" "Install the .NET 8 SDK: https://dotnet.microsoft.com/download"

if (Test-Path -LiteralPath $InstallDir) {
    if (-not (Test-Path -LiteralPath (Join-Path $InstallDir ".git"))) {
        throw "InstallDir already exists but is not a git repository: $InstallDir"
    }

    git -C $InstallDir fetch --prune
    git -C $InstallDir checkout main
    git -C $InstallDir pull --ff-only
} else {
    git clone $Repo $InstallDir
}

$dataDir = Join-Path $env:APPDATA "MenuPet"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

$settings = [ordered]@{
    ScalePercent = 100
    MovementSpeedPercent = 100
    SpeechText = $SpeechText
    RandomBenchPressEnabled = $true
    AppName = $Name
    PetImagePath = $null
}

if ($PetPath.Trim().Length -gt 0) {
    $source = Resolve-ExistingFile $PetPath
    if ([System.IO.Path]::GetExtension($source).ToLowerInvariant() -ne ".png") {
        throw "Pet image must be a PNG file: $source"
    }

    $petDestination = Join-Path $dataDir "pet-character.png"
    Copy-Item -LiteralPath $source -Destination $petDestination -Force

    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
    $destHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $petDestination).Hash
    if ($sourceHash -ne $destHash) {
        throw "PNG SHA-256 mismatch after copy."
    }

    $settings["PetImagePath"] = $petDestination
    Write-Host "Pet PNG SHA-256: $destHash"
} else {
    Write-Host "No pet PNG provided. Choose one from the tray menu after launch."
}

$settingsPath = Join-Path $dataDir "settings.json"
$settings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$project = Join-Path $InstallDir "windows\MenuPet.Windows\MenuPet.Windows.csproj"
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Windows project not found: $project"
}

$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$publishDir = Join-Path $desktop "$Name.Windows"
dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir

$exe = Join-Path $publishDir "MenuPet.Windows.exe"
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Build succeeded but executable was not found: $exe"
}

Write-Host "Installed: $publishDir"
Write-Host "Settings:  $settingsPath"

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
}
