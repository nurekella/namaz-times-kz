---
name: finish-task
description: Finish a task properly — verify, commit, log, record decisions, save state. Use when a task is implemented and has passed review.
---

# Finish task
1. Run the verification command from AGENTS.md, show the output. Failed — the task is not done.
2. `git diff --stat`, commit with a clear message (what and why) and an AI-author trailer: `AI-Assisted: claude` or `AI-Assisted: antigravity` (Claude Code may add its own `Co-Authored-By` — keep it). Don't push unless asked.
3. Problems and their fixes → one line in `docs/NOTES.md`: date — symptom → cause → fix → commit.
4. Architectural decision made → `docs/DECISIONS.md`: date — decision — why — alternatives.
5. Agent made the same mistake twice → propose a rule in AGENTS.md or a hook.
6. Run `checkpoint`.

## Red flags
- "Verification is slow, it obviously works" → run it.
- "No review needed, the change is tiny" → a tiny review is fast, do it.
- "I'll write NOTES later" → now, while you remember the cause.
