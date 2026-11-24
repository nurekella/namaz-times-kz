# STATE
> Save point for any agent. Read first. ≤ 60 lines. Updated by skill `checkpoint`.

**Updated:** 2026-11-24 — [claude] standard fixes, hook updated

## Stage
1. Released v0.15.0 (Wintor-style design), owner confirmed it works
2. ai-standard adopted, merged into `main`, verified after restart (hooks active, jq on PATH)
3. Fixes to the standard repo (`nurekella/ai-standard-for-coding`) ← NOW

## Done
- Standard PR #1 merged (fc21824): protect-secrets fails closed without jq; test.sh python check; audit.sh STATE fix
- Project hook updated from the standard (2936ecf, pushed): `.env` blocked with and without jq
- Standard PR #2 opened: `new-project.sh` runs `sed -i` only while placeholders remain + test

## In progress
- [claude] PR #2 branch `fix/sed-only-placeholders` in ~/ai-standard: adding VERSION 1.5.2 + CHANGELOG + README badge
  (PR #1 was merged without them — violated the standard repo's AGENTS.md rule 1); reviewer subagent running

## Next step
- Commit + push the version bump to PR #2; owner decides on merging PR #2
- After merge: `~/ai-standard/new-project.sh .` here (manifest → 1.5.2), commit only hook/manifest changes
- App ideas not started: accent colour choice in settings; tabs under the Today table; open the main window at startup

## Blockers
- none

## Last verification
- 2026-11-24: standard `tests/test.sh` → 50 ok, 2 FAIL (symlinks, known Windows limitation);
  project hook: `x/.env` → 2 with and without jq; `audit.sh .` → 0 issues
