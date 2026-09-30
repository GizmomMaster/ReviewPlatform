# ReviewPlatform

Платформа оценки компетенций по модели 360.

- [Техническое задание](tz.md)
- [План реализации](docs/implementation-plan.md)

## Структура

| Каталог | Содержимое |
|---|---|
| `backend/` | REST API на .NET 10 (Clean Architecture) |
| `frontend/` | SPA на React 19 + Vite + Tailwind + shadcn/ui |
| `seed/` | Начальная матрица компетенций и скрипт её сборки |
| `scripts/` | Обёртки для запуска npm и docker compose в контейнерах |

## Запуск

### Linux-сервер

Нужен только Docker. Локальные Node.js и плагин `docker compose` не обязательны — скрипты в `scripts/` запускают их в контейнерах.

```sh
cp .env.example .env          # при необходимости поменяйте порты и пароли
scripts/compose.sh up -d --build
```

### Windows и macOS

Нужен [Docker Desktop](https://www.docker.com/products/docker-desktop/) (на Windows — с бэкендом WSL 2, он предлагается при установке) и Git. Node.js и .NET ставить не нужно — всё собирается в контейнерах. Подходят и Intel, и Apple Silicon: все образы мультиархитектурные.

1. Запустите Docker Desktop и дождитесь статуса «Engine running».
2. Склонируйте репозиторий и перейдите в него:
   ```sh
   git clone git@github.com:GizmomMaster/ReviewPlatform.git
   cd ReviewPlatform
   ```
3. Создайте `.env` из примера (можно пропустить — тогда берутся значения по умолчанию):
   * macOS (Terminal): `cp .env.example .env`
   * Windows (PowerShell): `Copy-Item .env.example .env`
4. Соберите и запустите (первая сборка — 5–10 минут):
   ```sh
   docker compose up -d --build
   ```
5. Откройте http://localhost:8080. Адреса остальных сервисов — в таблице ниже.

Скрипты `scripts/*.sh` на Windows не нужны: в Docker Desktop уже есть `docker compose`, поэтому везде, где в README написано `scripts/compose.sh`, используйте `docker compose`. Скрипт демо-данных запускается Python 3: на macOS — `python3 scripts/seed-demo.py`, на Windows — `py scripts\seed-demo.py`.

Остановить: `docker compose down` (данные сохраняются), удалить вместе с базой: `docker compose down -v`.

**Если что-то не работает:**
* **Порт занят** (`port is already allocated`) — поменяйте `FRONTEND_PORT`, `BACKEND_PORT`, `POSTGRES_PORT` или `MAILPIT_UI_PORT` в `.env` и запустите заново.
* **Сборка бэкенда падает с нехваткой памяти** — в Docker Desktop → Settings → Resources выделите не меньше 4 ГБ.
* Логи сервиса: `docker compose logs -f backend`.

### Адреса и вход

| Сервис | Адрес |
|---|---|
| Приложение | http://localhost:8080 |
| API | http://localhost:5080 (документация: `/scalar/v1`), только локально |
| Mailpit (письма) | http://localhost:8025, только локально |
| PostgreSQL | localhost:55432, только локально |

Наружу открыто только приложение (порт 8080). Если стенд на сервере, остальное открывайте через SSH-туннель, например `ssh -L 8025:localhost:8025 <сервер>`.

**Вход в режиме разработки:** `admin@example.com` / `Admin123!`. Это первый администратор, его создаёт бэкенд при пустой БД из `appsettings.Development.json`. При первом входе система попросит сменить пароль.

**Демо-данные:** `scripts/seed-demo.py` через API заводит 10 вымышленных сотрудников с грейдами E1–E8 и две сессии с уже пройденными анкетами (статус «Ждёт решения»), чтобы сразу посмотреть отчёт и форму решения. Пароль администратора скрипт спросит (или `REVIEW_ADMIN_PASSWORD`); повторный запуск ничего не дублирует.

### Продакшен

```sh
cp .env.example .env          # раскомментируйте и заполните блок «Продакшен»
scripts/compose.sh -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

`docker-compose.prod.yml` включает `ASPNETCORE_ENVIRONMENT=Production`, берёт секреты только из окружения (без обязательных переменных compose не запустится), убирает Mailpit и закрывает внешние порты PostgreSQL и API — наружу открыт только nginx фронтенда. HTTPS завершается на внешнем прокси или балансировщике; из-за `Auth__SecureCookies=true` без HTTPS вход не работает.

| Переменная `.env` | Назначение |
|---|---|
| `POSTGRES_PASSWORD` | Пароль БД |
| `FRONTEND_BASE_URL` | Публичный адрес приложения — из него строятся ссылки в письмах |
| `JWT_SIGNING_KEY` | Секрет подписи токенов, не короче 32 символов (`openssl rand -base64 48`) |
| `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD` | Первый администратор: создаётся только в пустой БД, при первом входе меняет пароль |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY`, `SMTP_USERNAME`, `SMTP_PASSWORD` | Почтовый сервер |
| `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME` | Отправитель писем |

Без compose те же настройки задаются переменными окружения бэкенда в формате ASP.NET: `ConnectionStrings__Default`, `Jwt__SigningKey`, `Smtp__Host`, `Email__FromAddress` и т. д. `Cors__AllowedOrigins__0` нужен, только если фронтенд на другом домене.

**Логи.** Бэкенд пишет в stdout JSON (одно событие на строку, формат Serilog compact) — удобно для Loki, ELK и т. п. Уровни — секция `Serilog:MinimumLevel`, например `Serilog__MinimumLevel__Default=Warning`. Токены анкет в логах API и nginx маскируются (`/api/surveys/***`). За nginx реальный адрес клиента берётся из `X-Forwarded-For` — от него считаются лимиты на вход и анкеты.

**Резервные копии.** Сервис `backup` раз в сутки делает `pg_dump` в `./backups` (`BACKUP_DIR`) и хранит копии 14 дней (`BACKUP_KEEP_DAYS`). Каталог стоит регулярно копировать на другой сервер. Восстановление (текущие данные заменяются):

```sh
scripts/restore.sh backups/reviewplatform-20261001-030000.dump
```

## Разработка

**Backend** (нужен .NET SDK 10):

```sh
cd backend
dotnet build
dotnet test
dotnet run --project src/ReviewPlatform.Api   # http://localhost:5080, нужен Postgres из compose
```

При старте API применяет миграции и заполняет справочники и матрицу из `seed/backend-matrix.xlsx` (если матрица пуста). Интеграционные тесты поднимают свой Postgres через Testcontainers — нужен запущенный Docker.

Новая миграция:

```sh
dotnet tool restore
dotnet ef migrations add <Name> -p src/ReviewPlatform.Infrastructure -s src/ReviewPlatform.Api -o Persistence/Migrations
```

**Frontend** (npm в контейнере Node 22):

```sh
scripts/npm.sh install
scripts/npm.sh run lint
scripts/npm.sh run build
scripts/compose.sh --profile dev up frontend-dev   # Vite с hot reload: http://localhost:5173
scripts/npm.sh run gen:api                          # типы API из OpenAPI (бэкенд должен быть запущен)
```

**Сквозные тесты** (Playwright) проходят сценарий «запуск → опрос с телефона → отчёт → решение» и правку матрицы против запущенного стека, письма проверяются через Mailpit:

```sh
scripts/compose.sh up -d --build
scripts/e2e.sh                     # или scripts/e2e.sh e2e/matrix.spec.ts
```

Тесты входят первым администратором (`E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD`, по умолчанию dev-учётка) и при первом прогоне меняют его временный пароль на `<пароль>e2e`. В CI они запускаются после сборки образов.
