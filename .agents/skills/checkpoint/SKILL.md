---
name: checkpoint
description: Save the current project state to docs/STATE.md. Use when a step is finished, before context compaction, at the end of a session, or when the context is nearly full.
---

# Checkpoint
1. Rewrite `docs/STATE.md` in full (replace stale content, don't append):
   date and who; stage (← NOW); Done (outcomes only); In progress (files, branches, processes); Next step (1–3 concrete actions); Blockers; Last verification (command + result).
2. ≤ 60 lines. Details → `docs/NOTES.md`, decisions → `docs/DECISIONS.md`.
3. Uncommitted changes — commit them or list them under "In progress".
4. Reply in one line: "STATE saved: <stage> → <next step>".
