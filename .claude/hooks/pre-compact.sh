#!/usr/bin/env bash
# PreCompact: objective state snapshot before context compaction (no model involved).
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
mkdir -p docs
{
  echo "Snapshot: $(date '+%F %T')"
  if git rev-parse --git-dir >/dev/null 2>&1; then
    echo "Branch: $(git branch --show-current)"
    echo "Changed files:"; git status --short | head -30
    echo "Last commit: $(git log --oneline -1)"
  fi
  echo "tmux: $(tmux ls 2>/dev/null | tr '\n' ';')"
  last=$(ls -t logs/*.log 2>/dev/null | head -1)
  [ -n "$last" ] && { echo "Log: $last"; tail -n 3 "$last"; }
} > docs/.snapshot.md
exit 0
