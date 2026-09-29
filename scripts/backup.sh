#!/bin/sh
# Периодический pg_dump в /backups (сервис backup в docker-compose.yml). Старые копии удаляются.
# Переменные: PGHOST, PGDATABASE, PGUSER, PGPASSWORD — подключение; BACKUP_INTERVAL_HOURS (24), BACKUP_KEEP_DAYS (14).
set -eu
INTERVAL_HOURS="${BACKUP_INTERVAL_HOURS:-24}"
KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"

backup() {
  file="/backups/${PGDATABASE}-$(date -u +%Y%m%d-%H%M%S).dump"
  # Пишем во временный файл: оборванная копия не должна выглядеть готовой
  if pg_dump --format=custom --no-owner --file="$file.tmp"; then
    mv "$file.tmp" "$file"
    echo "backup: $file ($(du -h "$file" | cut -f1))"
  else
    rm -f "$file.tmp"
    echo "backup: pg_dump failed" >&2
  fi
  find /backups -name "${PGDATABASE}-*.dump" -mtime +"$KEEP_DAYS" -print -delete
}

until pg_isready -q; do sleep 2; done
while true; do
  backup
  sleep "$((INTERVAL_HOURS * 3600))"
done
