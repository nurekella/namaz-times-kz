---
name: worktree
description: Create a separate working folder (git worktree) for a parallel task or another AI, merge it back and remove it. Use when two agents work on the project at once or a parallel task is needed.
argument-hint: "new <task> [claude|ag] | merge <task> | list"
---

# Git worktree for parallel work

The main project folder is on `main` (or `master`) and one agent works there. Each parallel task gets its own folder next to it.

## new <task> [claude|ag]
```bash
p=ag; [ "$2" = claude ] && p=cl         # branch prefix: ag/… or cl/…
root=$(git rev-parse --show-toplevel); name=$(basename "$root")
git -C "$root" worktree add "../$name-$1" -b "$p/$1"
ln -sfn ../.agents/skills "../$name-$1/.claude/skills" 2>/dev/null || true
```
Record in `docs/STATE.md` of the main folder: `- [claude|antigravity] <task> — branch $p/$1, folder ../$name-$1`.
Tell the human which folder to open in which AI.

## merge <task>
1. In the task folder: verification from AGENTS.md passed, everything committed.
2. Review the diff by the other agent or subagent `reviewer`: `git diff main...<branch>`.
3. In the main folder:
```bash
git merge --no-ff <branch> -m "merge: <task>"
git worktree remove ../<name>-<task>
git branch -d <branch>
```
4. Conflict — don't resolve blindly: show the conflicting files to the human.
5. Update STATE.md (move the task from "In progress" to "Done").

## list
```bash
git worktree list
```

## Rules
- Never remove a worktree with uncommitted changes (`git -C <folder> status`).
- Don't copy secrets or `.env` into a worktree.
