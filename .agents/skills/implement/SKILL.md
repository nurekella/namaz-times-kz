---
name: implement
description: Implement docs/SPEC.md task by task — thin slice, verify, commit — until the result fully matches the SPEC (converged). Use when SPEC.md is ready and has a task list.
---

# Implement (spec → tasks → implement → converge)

## Process
1. Read `docs/SPEC.md`. No "Tasks" section with acceptance criteria — first split the plan into tasks (each ≤ ~200 lines of diff, verifiable) and write them to SPEC.
2. For each task in order:
   - first a check that currently fails (test, `--check`, smoke script), where possible;
   - the smallest change that makes it pass;
   - verification from AGENTS.md → show output;
   - commit `<type>: <task>` + `AI-Assisted:`; tick `[x]` in SPEC.
3. **Budget:** at most 3 attempts per task. Still failing — stop, record the blocker and what you tried in STATE.md, ask the human.
4. **Converge:** after all tasks — final end-to-end verification from SPEC + skill `review`. Blocker/major findings → new tasks in SPEC, repeat. None → "Converged", `finish-task`.

## Red flags (if you catch yourself thinking this — stop)
- "I'll verify at the end" → verify after every task.
- "While I'm here I'll fix this too" → out of SPEC scope, separate task.
- "This test is in the way — I'll weaken/remove it" → not without human approval.
- "Looks like it works" → show verification output.

## Verification
All tasks `[x]`, final verification passed (output shown), review has no blocker/major.
