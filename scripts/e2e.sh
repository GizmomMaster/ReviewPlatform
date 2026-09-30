#!/usr/bin/env sh
# Сквозные тесты Playwright в одноразовом контейнере против запущенного стека (scripts/compose.sh up -d).
# Chromium кешируется в ~/.cache/ms-playwright, его системные зависимости ставятся на время прогона.
# Адреса: E2E_BASE_URL (http://localhost:18080), E2E_MAILPIT_URL (http://localhost:18025). Аргументы передаются в playwright test.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BROWSERS="${PLAYWRIGHT_CACHE_DIR:-$HOME/.cache/ms-playwright}"
mkdir -p "$BROWSERS"
exec docker run --rm --network host \
  -e HOST_UID="$(id -u)" -e HOST_GID="$(id -g)" \
  -e PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
  -e E2E_BASE_URL="${E2E_BASE_URL:-http://localhost:18080}" \
  -e E2E_MAILPIT_URL="${E2E_MAILPIT_URL:-http://localhost:18025}" \
  -e E2E_ADMIN_EMAIL -e E2E_ADMIN_PASSWORD \
  -v "$BROWSERS:/ms-playwright" \
  -v "$ROOT/frontend:/app" \
  -w /app \
  node:22-bookworm-slim sh -c '
    trap "chown -R $HOST_UID:$HOST_GID /ms-playwright /app/test-results /app/playwright-report 2>/dev/null || true" EXIT
    npx playwright install --with-deps --only-shell chromium > /tmp/install.log 2>&1 || { tail -20 /tmp/install.log; exit 1; }
    npx playwright test "$@"' e2e "$@"
