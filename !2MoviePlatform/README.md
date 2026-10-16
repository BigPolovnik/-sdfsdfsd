# Кинотека — тестовая версия 0.2

Русскоязычный адаптивный интерфейс онлайн-кинотеатра и серверный импорт каталога Vibix.

## Что уже реализовано

- React + TypeScript + Vite: главная страница, каталог, поиск, фильтры, карточки, подробная карточка и встраиваемый плеер Vibix.
- ASP.NET Core / C#: API каталога, пагинация и серверная синхронизация Vibix.
- PostgreSQL: хранение импортированных метаданных фильмов и сериалов.
- Vibix: Bearer-токен используется только на backend; импорт идёт страницами через актуальный документированный метод `/api/v1/publisher/videos/links`, каждая страница сохраняется в PostgreSQL до запроса следующей.
- Админ-эндпоинты для проверки статуса и ручной синхронизации защищены отдельным `X-Admin-Key`.
- Периодический импорт можно включить через `VIBIX_AUTOSYNC=true` (интервал по умолчанию 6 часов).
- Docker Compose: frontend, API, PostgreSQL, Redis.

## Быстрый запуск на Windows 11 или Linux

1. Установите Docker Desktop (Windows) или Docker Engine + Compose (Linux).
2. В каталоге проекта скопируйте `.env.example` в `.env`.
3. Откройте `.env`, задайте уникальные значения `DB_PASSWORD`, `JWT_KEY`, `ADMIN_KEY` и вставьте новый `VIBIX_API_TOKEN` из кабинета Vibix.
4. В каталоге с `docker-compose.yml` выполните:

   ```bash
   docker compose up -d --build
   ```

5. Откройте `http://localhost:3000`.
6. Нажмите ⚙ в правом верхнем углу, введите значение `ADMIN_KEY` из `.env` и нажмите «Синхронизировать каталог».
7. Логи API: `docker compose logs -f api`. Состояние контейнеров: `docker compose ps`.

На Windows 11, если Docker Desktop пишет `Virtual Machine Platform not enabled`, включите компонент в PowerShell от имени администратора:
`Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform`, затем перезагрузите Windows. После перезагрузки запустите Docker Desktop и повторите команду Compose.

На сервере сайт будет доступен по адресу сервера на порту 3000. Для публичного домена перед ним следует настроить HTTPS через Caddy или Nginx.

## Vibix API

Документация: https://vibix.org/api/external/documentation

Используемый метод сверён с приложенной OpenAPI-документацией 1.1.0: `GET https://vibix.org/api/v1/publisher/videos/links?page=N&limit=50&is_uploaded=true`, заголовок `Authorization: Bearer <token>`. Ответ содержит массив `data`; пагинация задаётся параметрами `page` и `limit`. Документация перечисляет поля `id`, `type`, `year`, `kp_id`, рейтинги, `iframe_url`, `poster_url`, `backdrop_url`, `voiceovers`, `genre`, `country`, описание и другие метаданные.

Поля карточки маппятся из документированного ответа: `name`, `name_rus`, `name_eng`, `name_original`, `description`, `description_short`, `poster_url`, `backdrop_url`, `kp_id`, `kp_rating`, `imdb_rating`, `genre`, `year`, `country`, `quality`, `voiceovers`. Значения года и рейтингов обрабатываются как числа или строки, поскольку в примерах схемы они представлены числовыми типами. Метод деталей `/publisher/videos/kp/{kpId}` в документации возвращает `id` и `iframe_url`; реализация поддерживает как это поле `id`, так и `iframe_video_id` из прежнего контракта. Если форма ответа отличается от схемы, подробность ошибки будет в логах API.

Плеер использует HTTPS `iframe_url`, возвращённый Vibix, если он есть; иначе использует SDK Vibix с идентификатором видео. Прямые ссылки на видеопотоки не извлекаются.

## Что нужно проверить в этой первой сборке

- В кабинете Vibix включён доступ к Publisher API и токен активен.
- Токен имеет доступ к `/api/v1/publisher/videos/links` и `/api/v1/publisher/videos/kp/{kpId}`.
- Ответ `/videos/links` содержит `data`; детальный запрос по Kinopoisk ID возвращает `id` и `iframe_url` согласно приложенной документации.
- Для мультфильмов Vibix возвращает жанры/теги с маркерами анимации; сейчас категория определяется по этим значениям.
- Автоматическая синхронизация выполняется после старта с задержкой 20 секунд, затем по заданному интервалу.

## Ограничения текущей версии

- Это первая тестовая сборка, а не production-релиз. Живой запрос к Vibix из этой среды не выполнен, поэтому реальный ответ API необходимо проверить у тебя после запуска.
- Импорт работает в фоне постранично: страница сохраняется в БД сразу после обработки, поэтому сайт не ждёт окончания всего каталога. При ошибке уже сохранённые страницы остаются доступными. Импорт структуры сезонов/эпизодов пока не реализован отдельным запросом.
- Авторизация пользователей, избранное и история просмотра пока не подключены к интерфейсу.
- При изменении схемы существующей базы данных может потребоваться пересоздать тестовый volume PostgreSQL. Это удалит только данные в этом тестовом volume: `docker compose down -v` (не выполняйте на базе с нужными данными).

## Полезные URL

- Сайт: `http://localhost:3000`
- API health: `http://localhost:5000/health` (также доступен через frontend proxy)
- Swagger: `http://localhost:5000/swagger` или `http://localhost:3000/swagger`
- Каталог: `http://localhost:5000/api/catalog/movies`
- Админ-статус: `GET /api/admin/status` с заголовком `X-Admin-Key`
- Ручная синхронизация: `POST /api/admin/sync-vibix` с заголовком `X-Admin-Key`


### Исправление сборки v0.3

В `MoviePlatform.Application.csproj` добавлены ссылки на `MoviePlatform.Infrastructure`, `Microsoft.EntityFrameworkCore` и shared framework `Microsoft.AspNetCore.App`. Это устраняет ошибки компиляции `VibixCatalogService`, который использует `AppDbContext`, EF Core, `IOptions` и `ILogger`.

Если вы обновляете существующую папку проекта, замените файл `backend/MoviePlatform.Application/MoviePlatform.Application.csproj` версией из архива v0.3, затем выполните `docker compose up -d --build`.
