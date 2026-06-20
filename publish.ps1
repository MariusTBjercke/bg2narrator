# Builds a release zip for GitHub Releases (BG2EE game folder layout).
# Usage: .\publish.ps1
#        .\publish.ps1 -Version 0.1.0

param(
	[string]$Version = (
		[regex]::Match(
			(Get-Content "$PSScriptRoot\BG2Narrator\lua\M_BG2NAR.lua" -Raw),
			'BG2Narrator\.version = "([^"]+)"'
		).Groups[1].Value
	)
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$svcProject = Join-Path $root "src\NarratorSvc\NarratorSvc.csproj"
$svcOut = Join-Path $root "src\NarratorSvc\deploy\NarratorSvc"
$modSrc = Join-Path $root "BG2Narrator"
$stageDir = Join-Path $root "artifacts\BG2Narrator"
$zipPath = Join-Path $root "artifacts\BG2Narrator-v$Version.zip"

if (-not $Version) {
	Write-Error "Could not read BG2Narrator.version from BG2Narrator/lua/M_BG2NAR.lua"
}

Write-Host "Publishing BG2 Narrator v$Version..."

dotnet build $svcProject -c Release
if ($LASTEXITCODE -ne 0) {
	exit $LASTEXITCODE
}

if (Test-Path $stageDir) {
	Remove-Item $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stageDir "lua") | Out-Null
Copy-Item -Force (Join-Path $modSrc "BG2Narrator.tp2") $stageDir
Copy-Item -Force (Join-Path $modSrc "settings.example.json") $stageDir
Copy-Item -Force (Join-Path $modSrc "lua\M_BG2NAR.lua") (Join-Path $stageDir "lua\M_BG2NAR.lua")
Copy-Item -Force (Join-Path $svcOut "NarratorSvc.exe") $stageDir
Get-ChildItem $svcOut -Filter "*.dll" | ForEach-Object {
	Copy-Item -Force $_.FullName $stageDir
}
Get-ChildItem $stageDir -Filter "*.pdb" -Recurse | Remove-Item -Force

if (Test-Path $zipPath) {
	Remove-Item $zipPath -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts") | Out-Null
Compress-Archive -Path $stageDir -DestinationPath $zipPath

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "Done."
Write-Host "  Folder: $stageDir"
Write-Host "  Zip:    $zipPath ($sizeMb MB)"
Write-Host ""
Write-Host "Extract into your BG2EE install folder, then:"
Write-Host "  1. Copy settings.example.json to BG2Narrator/settings.json and add your ElevenLabs API key."
Write-Host "  2. Copy BG2Narrator.tp2 to the game folder and run setup-BG2Narrator.exe once."
Write-Host "  3. Launch BG2EE through InfinityLoader/EEex and run BG2Narrator/NarratorSvc.exe."
Write-Host ""
Write-Host "Next: tag and create a GitHub Release with the zip attached."
Write-Host "  git tag v$Version"
Write-Host "  git push origin v$Version"
Write-Host "  gh release create v$Version $zipPath --title ""BG2 Narrator v$Version"""

