#!/usr/bin/env sh
# docker compose; если плагин не установлен — запускается из образа docker:cli.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if docker compose version >/dev/null 2>&1; then
  cd "$ROOT" && exec docker compose "$@"
fi
TTY_FLAGS=""
[ -t 0 ] && TTY_FLAGS="-it"
exec docker run --rm $TTY_FLAGS \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v "$ROOT:$ROOT" \
  -w "$ROOT" \
  docker:cli compose "$@"
