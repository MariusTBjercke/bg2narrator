# BG2 Narrator

ElevenLabs voice narration for *Baldur's Gate II: Enhanced Edition* (and EET). Uses EEex hooks to capture dialogue and a sidecar (`NarratorSvc.exe`) for synthesis, caching, and playback.

## Requirements

- *Baldur's Gate II: Enhanced Edition* v2.6.x (or EET)
- EEex installed; launch via `InfinityLoader.exe` / `EEex.exe`
- .NET Framework 4.8 (for `NarratorSvc.exe`)
- [ElevenLabs](https://elevenlabs.io/) API key

## Quick start (developers)

```powershell
.\deploy.ps1 -Install -StartSvc
```

Then edit `{GameFolder}\BG2Narrator\settings.json` with your API key.

## Quick start (release zip)

1. Extract `BG2Narrator-vX.Y.Z.zip` into your BG2EE install folder.
2. Copy `BG2Narrator/settings.example.json` to `BG2Narrator/settings.json` and add your API key.
3. Run `setup-BG2Narrator.exe` once (WeiDU).
4. Start `BG2Narrator/NarratorSvc.exe`.
5. Launch the game through InfinityLoader/EEex.

## How it works

| Piece | Role |
|------|------|
| `BG2Narrator/lua/M_BG2NAR.lua` | EEex hooks + dialogue capture (8-char `M_` name required) |
| `NarratorSvc.exe` | TTS + cache + audio playback |
| `BG2Narrator/events.jsonl` | IPC queue (Lua writes, sidecar tails) |
| `BG2Narrator/settings.json` | API key, model, voice mappings |
| `BG2Narrator/cache/` | Local cached MP3 files |

BG2 uses `events.jsonl` IPC directly (unlike PST's Baldur.lua fallback).

## Settings

Copy `BG2Narrator/settings.example.json` to `settings.json`.

| Field | Purpose |
|------|------|
| `ApiKey` | ElevenLabs API key |
| `DefaultVoiceId` | Fallback voice |
| `ModelId` | ElevenLabs model (default `eleven_flash_v2_5`) |
| `SpeechSpeed` | Global speed multiplier |
| `Volume` | Playback volume `0..1` |
| `OnlyUnvoicedLines` | Skip lines with official VO |
| `VerboseLogging` | Extra sidecar logs |
| `VoiceMappings` | Per-speaker voice overrides |

## Deploy options

```powershell
.\deploy.ps1
.\deploy.ps1 -GamePath "D:\...\Baldur's Gate II Enhanced Edition"
.\deploy.ps1 -Install
.\deploy.ps1 -StartSvc
.\deploy.ps1 -SkipBuild
```

## Publish a GitHub release

```powershell
.\publish.ps1
git tag v0.1.0
git push origin v0.1.0
gh release create v0.1.0 artifacts/BG2Narrator-v0.1.0.zip --title "BG2 Narrator v0.1.0"
```

## Repository layout

```
BG2Narrator/           # WeiDU mod payload (lua/menu/tp2/settings example)
src/NarratorSvc/      # TTS sidecar source
deploy.ps1            # local install helper
publish.ps1           # release zip builder
```

## Debug flags (Lua)

Set in `BG2Narrator/lua/M_BG2NAR.lua` before deploy:

- `BG2Narrator.logAllActions = true`
- `BG2Narrator.debugUiProbe = true`
- `BG2Narrator.debugChoices = true`
- `BG2Narrator.debugDialogEnd = true`

