#!/usr/bin/env bash
# PreToolUse (Read|Edit|Write): blocks access to secret files. exit 2 = deny, stderr goes to Claude.
input=$(cat)
if command -v jq >/dev/null 2>&1; then
  path=$(jq -r '.tool_input.file_path // .tool_input.path // empty' <<<"$input")
else
  # no jq (e.g. Git Bash on Windows): crude fallback; never fail open
  path=$(sed -nE 's/.*"(file_path|path)" *: *"([^"]*)".*/\2/p' <<<"$input" | head -1)
  # undo the one escape every Windows path has (\\ → /); any other escape (., \") can't be decoded
  # without jq and could hide a name like .env — block instead of guessing
  path=${path//\\\\//}
  if { [ -z "$path" ] && grep -q '"tool_input"' <<<"$input"; } || [[ $path == *\\* ]]; then
    echo "Blocked: jq is not installed, so the path cannot be checked. Install jq." >&2
    exit 2
  fi
fi
[ -z "$path" ] && exit 0
name=$(basename "$path")
case "$name" in
  .env|.env.*|*.pem|*.key|*.p12|*.pfx|id_rsa*|id_ed25519*|*kubeconfig*|credentials*|*.kdbx)
    case "$name" in *.example|*.sample|*.template) exit 0 ;; esac
    echo "Blocked: $name looks like a secrets file. Ask the human if access is really needed." >&2
    exit 2 ;;
esac
case "$path" in */secrets/*|*/.ssh/*|*/.kube/*)
  echo "Blocked: $path is a secrets directory." >&2; exit 2 ;;
esac
exit 0
