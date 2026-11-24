# STATE
> Save point for any agent. Read first. ≤ 60 lines. Updated by skill `checkpoint`.

**Updated:** 2026-11-24 — [claude] ai-standard adoption committed

## Stage
1. Released v0.15.0 (Wintor-style design), owner confirmed it works
2. ai-standard v1.5.1 adopted on branch `ai-standard` (commit 37d3254), not merged, not pushed ← NOW

## Done
- AGENTS.md rewritten to the template (94 lines); skills `release`, `visual-check`; rule `design.md`
- docs/NOTES.md (pitfalls + Windows limitations), DECISIONS.md, STATE.md filled
- audit.sh: 0 issues, fixed context ~2561 tokens; 2 warnings (`.claude/skills|rules` are copies, not symlinks)

## In progress
- none

## Next step
- Owner decides: (1) add jq to the user PATH — protect-secrets hook fails open without it;
  (2) restore graphify / subagent / @RTK.md lines in the new global ~/.claude/CLAUDE.md (backup: CLAUDE.md.bak.2026-11-24-2309);
  (3) fixes to the standard repo: hook fail-closed without jq, test.sh python check, audit.sh line 74
- Then: merge `ai-standard` into `main` only if asked

## Blockers
- none

## Last verification
- 2026-11-24: `dotnet build -c Release` + `--selftest` → selftest ok; `audit.sh .` → 0 issues;
  protect-secrets with jq → exit 2 on `.env`; session-context prints STATE
