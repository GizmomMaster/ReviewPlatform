import { defineConfig, devices } from '@playwright/test'

/**
 * Сквозные тесты против запущенного приложения (по умолчанию — docker compose: http://localhost:18080).
 * Письма проверяются через API Mailpit (E2E_MAILPIT_URL), если он задан.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 180_000,
  expect: { timeout: 15_000 },
  // Тесты делят одну БД и лимиты запросов по IP — последовательно надёжнее
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:18080',
    locale: 'ru-RU',
    timezoneId: 'Europe/Moscow',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
