# NarratorSvc

TTS sidecar used by BG2Narrator (and shared with PSTNarrator). When launched from `{game}/BG2Narrator/`, it tails dialogue IPC from `events.jsonl` and plays ElevenLabs speech.

## Build

```powershell
dotnet build NarratorSvc.csproj -c Release
```

Output: `deploy/NarratorSvc/NarratorSvc.exe` (+ DLLs).

From the repo root, `.\deploy.ps1` or `.\publish.ps1` builds this project automatically.
