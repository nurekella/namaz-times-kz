# STATE
> Save point for any agent. Read first. ≤ 60 lines. Updated by skill `checkpoint`.

**Updated:** 2026-11-24 — [claude] ai-standard active after restart

## Stage
1. Released v0.15.0 (Wintor-style design), owner confirmed it works
2. ai-standard v1.5.1 adopted, merged into `main` (c63e363) and pushed; verified after restart ← NOW

## Done
- AGENTS.md to template; skills `release`, `visual-check`; rule `design.md`; NOTES/DECISIONS filled
- jq on the user PATH; global ~/.claude/CLAUDE.md has graphify / subagent / @RTK.md lines back
- Standard fixes pushed to `nurekella/ai-standard-for-coding`, branch `fix/windows-findings` (04ae34f), no PR yet

## In progress
- none

## Next step
- Owner: open/merge a PR for `fix/windows-findings`; then run `~/ai-standard/new-project.sh .` here to update the hook
- App ideas not started: accent colour choice in settings; tabs under the Today table; open the main window at startup

## Blockers
- none

## Last verification
- 2026-11-24 after restart: SessionStart hook printed PROJECT CONTEXT; `jq` 1.8.2 on PATH;
  protect-secrets on `x/.env` → exit 2; Read of `test/.env` denied
