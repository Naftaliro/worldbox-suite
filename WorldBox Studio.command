#!/usr/bin/env bash
cd "$(dirname "$0")"
echo "Starting WorldBox Studio at http://127.0.0.1:8765 ..."
python3 -m tools.studio.server --port 8765 &
SERVER_PID=$!
sleep 1.2
open "http://127.0.0.1:8765"
echo "WorldBox Studio is running (PID $SERVER_PID). Close this window or press Ctrl+C to stop."
wait $SERVER_PID
