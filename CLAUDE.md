@AGENTS.md
@docs/STATE.md

# Claude Code only
- Model `opusplan`; subagents in `.claude/agents/`; skills and rules mirror `.agents/` (copies on this Windows machine — re-copy after edits, see docs/NOTES.md).
- Windows, Git Bash: long-running processes — background tasks with a log, not tmux; Python is `py`, jq needs PATH (docs/NOTES.md).
- Research and large logs — via subagents (researcher, log-analyzer), not in the main context.
- At ~70% context: `/checkpoint`, then `/compact`. Between unrelated tasks — `/clear`.
- When compacting preserve: list of changed files, verification commands, next step.
