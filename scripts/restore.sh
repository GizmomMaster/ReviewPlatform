#!/usr/bin/env sh
# Восстановление БД из копии, сделанной сервисом backup: scripts/restore.sh backups/<файл>.dump
# Текущие данные будут заменены. Бэкенд на время восстановления останавливается.
set -e
FILE="$1"
[ -f "$FILE" ] || { echo "usage: $0 backups/<file>.dump" >&2; exit 1; }
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE="$ROOT/scripts/compose.sh"

printf 'Восстановить БД из %s? Текущие данные будут заменены. [y/N] ' "$FILE"
read -r answer
[ "$answer" = "y" ] || exit 1

"$COMPOSE" stop backend
"$COMPOSE" exec -T postgres sh -c 'pg_restore --clean --if-exists --no-owner -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < "$FILE"
"$COMPOSE" start backend
echo "Готово."
