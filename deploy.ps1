# Deploy BG2Narrator to a local BG2:EE install.
param(
	[string]$GamePath = "D:\SteamLibrary\steamapps\common\Baldur's Gate II Enhanced Edition",
	[switch]$Install,
	[switch]$StartSvc,
	[switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$svcRoot = Join-Path $root "src\NarratorSvc"
$modSrc = Join-Path $root "BG2Narrator"
$luaSrc = Join-Path $modSrc "lua\M_BG2NAR.lua"
$tp2Src = Join-Path $modSrc "BG2Narrator.tp2"
$svcProject = Join-Path $svcRoot "NarratorSvc.csproj"
$svcOut = Join-Path $svcRoot "deploy\NarratorSvc"
$svcExeName = "NarratorSvc.exe"
$settingsExample = Join-Path $modSrc "settings.example.json"

function Remove-StaleOverrideFiles {
	param([string]$Root)
	foreach ($staleOverride in @("override\M_BG2Narr.lua", "override\BG2NarrT.menu")) {
		$stalePath = Join-Path $Root $staleOverride
		if (Test-Path $stalePath) {
			Remove-Item -Force $stalePath
			Write-Host "Removed stale override file: $stalePath"
		}
	}
}

if (-not (Test-Path $GamePath)) {
	Write-Error "Game folder not found: $GamePath"
}

if (-not (Test-Path $luaSrc)) {
	Write-Error "Missing source file: $luaSrc"
}

if (-not (Test-Path $tp2Src)) {
	Write-Error "Missing source file: $tp2Src"
}

if (-not $SkipBuild) {
	if (-not (Test-Path $svcProject)) {
		Write-Error "Missing sidecar project: $svcProject"
	}

	Write-Host "Building NarratorSvc..."
	dotnet build $svcProject -c Release
	if ($LASTEXITCODE -ne 0) {
		exit $LASTEXITCODE
	}
}

$modDest = Join-Path $GamePath "BG2Narrator"
$luaDestDir = Join-Path $modDest "lua"
$overrideLua = Join-Path $GamePath "override\M_BG2NAR.lua"
$settingsDest = Join-Path $modDest "settings.json"

New-Item -ItemType Directory -Force -Path $luaDestDir | Out-Null
Copy-Item -Force $tp2Src $modDest
Copy-Item -Force $luaSrc (Join-Path $luaDestDir "M_BG2NAR.lua")
Copy-Item -Force $luaSrc $overrideLua
Remove-StaleOverrideFiles -Root $GamePath

if (Test-Path $svcOut) {
	Copy-Item -Force (Join-Path $svcOut $svcExeName) $modDest
	Get-ChildItem $svcOut -Filter "*.dll" | ForEach-Object {
		Copy-Item -Force $_.FullName $modDest
	}

	foreach ($legacy in @("BG2NarratorSvc.exe", "PSTNarratorSvc.exe")) {
		$legacyPath = Join-Path $modDest $legacy
		if (Test-Path $legacyPath) {
			Remove-Item -Force $legacyPath
		}
	}
}

if (Test-Path $settingsExample) {
	if (-not (Test-Path $settingsDest)) {
		Copy-Item -Force $settingsExample $settingsDest
		Write-Host "Created default settings: $settingsDest"
	}
}

Write-Host "Deployed BG2Narrator to $GamePath"
Write-Host "  Mod folder : $modDest"
Write-Host "  Active lua : $overrideLua"
Write-Host "  Sidecar    : $(Join-Path $modDest $svcExeName)"

if ($Install) {
	$setupExe = Join-Path $GamePath "setup-BG2Narrator.exe"
	if (-not (Test-Path $setupExe)) {
		Write-Error "WeiDU setup not found: $setupExe`nCopy or rename a WeiDU executable to setup-BG2Narrator.exe in the game folder."
	}

	Write-Host "Running WeiDU install..."
	Push-Location $GamePath
	try {
		& $setupExe --no-exit-pause --force-install-list 0
	} finally {
		Pop-Location
	}

	Remove-StaleOverrideFiles -Root $GamePath
}

if ($StartSvc) {
	$svcExe = Join-Path $modDest $svcExeName
	if (-not (Test-Path $svcExe)) {
		Write-Error "Sidecar executable not found: $svcExe"
	}

	Write-Host "Starting NarratorSvc..."
	Start-Process -FilePath $svcExe -ArgumentList @("--game", $GamePath) -WorkingDirectory $modDest
}

Write-Host ""
Write-Host "1. Edit BG2Narrator/settings.json with your ElevenLabs API key."
Write-Host "2. Start NarratorSvc.exe (or rerun deploy.ps1 -StartSvc)."
Write-Host "3. Launch BG2EE through InfinityLoader.exe / EEex.exe."
Write-Host "4. Talk to an unvoiced NPC and listen for TTS."

