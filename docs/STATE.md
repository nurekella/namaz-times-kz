# STATE
> Save point for any agent. Read first. ≤ 60 lines. Updated by skill `checkpoint`.

**Updated:** 2026-11-24 — [claude] project on ai-standard 1.5.2

## Stage
1. Released v0.15.0 (Wintor-style design), owner confirmed it works
2. ai-standard adopted, merged into `main`, verified after restart (hooks active, jq on PATH)
3. Fixes to the standard repo merged (PR #1, #2 → v1.5.2); project updated to 1.5.2 ← NOW

## Done
- Standard PR #1 merged (fc21824): protect-secrets fails closed without jq; test.sh python check; audit.sh STATE fix
- Project hook updated from the standard (2936ecf, pushed): `.env` blocked with and without jq
- Standard PR #2 merged (f679d85, v1.5.2): `sed -i` only while placeholders remain; hook blocks undecodable escapes without jq
- Project updated to standard 1.5.2 (hook + manifest only; seed files untouched)

## In progress
- none

## Next step
- App ideas not started: accent colour choice in settings; tabs under the Today table; open the main window at startup
- Optional: symlinks on Windows (Developer Mode + `MSYS=winsymlinks:nativestrict`) to drop the 2 audit warnings

## Blockers
- none

## Last verification
- 2026-11-24: standard CI green (Linux); local `tests/test.sh` → 50 ok, 2 FAIL (symlinks, Windows only);
  project hook: `x/.env` → 2 with and without jq; `audit.sh .` → 0 issues
