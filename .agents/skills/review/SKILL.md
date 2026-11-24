---
name: review
description: Independent review of changes in a clean context against SPEC/plan. Use when implementation is finished, before treating the task as done.
---

# Review
0. Deterministic first (code, not opinion): verification from AGENTS.md, linters, `~/ai-standard/tools/audit.sh .`. Fails → blocker.
1. Collect: `git diff` (or branch diff), `docs/SPEC.md` or the plan, the verification command.
2. Hand it to subagent `reviewer` (Claude Code) or open a new session. The reviewer sees only the diff and criteria, not the reasoning.
3. Check:
   - all requirements implemented, nothing out of scope changed
   - edge cases and rollback
   - secrets, hard-coded credentials, dangerous prod commands
   - verification was run and passed (output)
4. Flag only what affects correctness and requirements. Style is optional — don't inflate.
5. Precision over recall: only confident findings, each with an existing `file:line` and a `[blocker|major|minor]` label.
6. Result: list of findings and a verdict "ok to merge" / "needs fixes".
