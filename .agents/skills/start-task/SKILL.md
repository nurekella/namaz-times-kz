---
name: start-task
description: Start a new task properly — restore context, check repo state, state the goal and definition of done. Use when a new task or session begins.
---

# Start task
1. Read `docs/STATE.md`, the last 15 lines of `docs/NOTES.md`, `git status`, `git log --oneline -5`.
2. Uncommitted changes unrelated to the task — report them and ask what to do.
3. State in 3 lines: goal, affected files/systems, **how we verify it's done** (a command).
4. Need credentials, access or hosts — ask, don't assume.
5. Task touches > 1 file or prod — switch to planning (plan mode) and show the plan before changing anything.
6. Task is large and unclear — suggest `spec-interview`.

## Red flags
- "The task is clear, no plan needed" with > 1 file or prod → plan.
- "Credentials are probably the same as last time" → ask.
- "I'll figure out verification later" → don't start without a verification command.
