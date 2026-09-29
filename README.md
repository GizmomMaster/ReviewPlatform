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

Нужен только Docker. Локальные Node.js и плагин `docker compose` не обязательны — скрипты в `scripts/` запускают их в контейнерах.

```sh
cp .env.example .env          # при необходимости поменяйте порты и пароли
scripts/compose.sh up -d --build
```

| Сервис | Адрес |
|---|---|
| Приложение | http://localhost:8080 |
| API | http://localhost:5080 (документация: `/scalar/v1`) |
| Mailpit (письма) | http://localhost:8025 |
| PostgreSQL | localhost:55432 |

## Разработка

**Backend** (нужен .NET SDK 10):

```sh
cd backend
dotnet build
dotnet test
dotnet run --project src/ReviewPlatform.Api   # http://localhost:5080
```

**Frontend** (npm в контейнере Node 22):

```sh
scripts/npm.sh install
scripts/npm.sh run lint
scripts/npm.sh run build
scripts/compose.sh --profile dev up frontend-dev   # Vite с hot reload: http://localhost:5173
scripts/npm.sh run gen:api                          # типы API из OpenAPI (бэкенд должен быть запущен)
```
