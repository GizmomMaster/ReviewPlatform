#!/usr/bin/env sh
# Запускает npm/npx для frontend в контейнере Node 22 (локальный Node не требуется).
# Примеры: scripts/npm.sh install; scripts/npm.sh run build; scripts/npm.sh exec -- shadcn add button
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "${NPM_CACHE_DIR:-$HOME/.npm}"
TTY_FLAGS=""
[ -t 0 ] && TTY_FLAGS="-it"
exec docker run --rm $TTY_FLAGS \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e npm_config_cache=/tmp/.npm \
  -v "$ROOT/frontend:/app" \
  -v "${NPM_CACHE_DIR:-$HOME/.npm}:/tmp/.npm" \
  -w /app \
  --network host \
  node:22-alpine npm "$@"
