# STATE
> Save point for any agent. Read first. ≤ 60 lines. Updated by skill `checkpoint`.

**Updated:** 2026-11-24 — [claude] adopt ai-standard v1.5.1

## Stage
1. Released v0.15.0 (Wintor-style design), owner confirmed it works
2. Adopting ai-standard on branch `ai-standard` ← NOW (test of the standard, not merged)

## Done
- v0.15.0 on GitHub Releases (exe + msi); AGENTS.md rewritten to the standard's template
- Skills added: `release`, `visual-check`; rule `design.md`; NOTES/DECISIONS filled from history

## In progress
- [claude] ai-standard adoption — branch `ai-standard`, uncommitted until the owner says "yes"

## Next step
- Owner: review branch `ai-standard`, decide whether to merge into `main`
- Ideas not started: accent colour choice in settings; tabs under the Today table; open the main window at startup

## Blockers
- none

## Last verification
- `dotnet build -c Release` + `--selftest` → selftest ok (v0.15.0, 2026-11-10)
