#!/usr/bin/env bash
# PreToolUse (Read|Edit|Write): blocks access to secret files. exit 2 = deny, stderr goes to Claude.
path=$(jq -r '.tool_input.file_path // .tool_input.path // empty')
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
