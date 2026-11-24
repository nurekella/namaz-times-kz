#!/usr/bin/env bash
# SessionStart: stdout is added to Claude's context. Restores project state.
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
echo "=== PROJECT CONTEXT: $(basename "$PWD") ==="
echo "Rules — AGENTS.md (already loaded). Current state:"
echo
[ -f docs/STATE.md ] && cat docs/STATE.md
echo
echo "--- NOTES (latest) ---"
[ -f docs/NOTES.md ] && tail -n 10 docs/NOTES.md
if [ -f docs/.snapshot.md ]; then
  echo; echo "--- Snapshot before last compaction ---"; cat docs/.snapshot.md
fi
if git rev-parse --git-dir >/dev/null 2>&1; then
  echo; echo "--- git ---"
  git log --oneline -5 2>/dev/null
  git status --short 2>/dev/null | head -15
fi
tmux ls 2>/dev/null | sed 's/^/tmux: /'
echo
echo "Continue from \"Next step\" in STATE.md. Do not start over."
