#!/usr/bin/env bash
# Sends commands to the running prototype through the debug bridge (D-023) and prints the JSON reply.
#
#   tools/gamebridge/play.sh "select 7; walk 120 85; click 121 85; state"
#   tools/gamebridge/play.sh < tools/gamebridge/scripts/recorrido.txt
#   PORT=7778 tools/gamebridge/play.sh state
#
# The game must be in Play mode (editor) or running as a development build.
set -euo pipefail
PORT="${PORT:-7777}"
if [[ $# -gt 0 ]]; then
    BODY="$*"
else
    BODY="$(cat)"
fi
curl -sS --max-time 330 -X POST --data-binary "$BODY" -H "Content-Type: text/plain; charset=utf-8" "http://localhost:${PORT}/"
echo
