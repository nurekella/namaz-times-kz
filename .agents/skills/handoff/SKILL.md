---
name: handoff
description: Hand a task over to another AI (Claude Code ↔ Antigravity) or take one over — STATE.md, commit, instructions for the next agent. Use when the human switches AI or says "hand over", "take over", "continue after the other agent".
---

# Handoff between AIs

Identify yourself: Claude Code → `[claude]`, Antigravity → `[antigravity]`.

## Hand over
1. Verify: run the verification command from AGENTS.md, record the result.
2. `docs/STATE.md` (as in `checkpoint`), plus under "In progress":
   `- [to] <task> — branch <branch>, folder <path> — HANDED OVER by [me] <date>`
   and a "For the next agent" block: what's done, what's left, gotchas, how to verify.
   The block must be self-contained: the other agent doesn't see this chat. Make sure it has a definition of done, a verification command and what's out of scope.
3. Commit everything: `handoff: <task> → [to]`. Don't push.
4. Give the human one line of instructions for the other AI:
   `Open <folder>, read docs/STATE.md, take over task "<task>" via skill handoff.`

## Take over
1. Read `docs/STATE.md`, find the task tagged for you. No tag — ask the human.
2. `git status`, `git log --oneline -5` — confirm the branch and folder match.
3. Replace the tag: `- [me] <task> — branch …, folder … — taken over <date>`; remove the "For the next agent" block once read.
4. Continue from "Next step".

## Rules
- Don't touch a task tagged `[claude]` / `[antigravity]` by someone else without a handoff.
- Two agents at the same time — only in separate folders (skill `worktree`).
