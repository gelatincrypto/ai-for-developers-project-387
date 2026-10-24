# Календарь звонков


[![hexlet-check](https://github.com/gelatincrypto/ai-for-developers-project-387/actions/workflows/hexlet-check.yml/badge.svg)](https://github.com/gelatincrypto/ai-for-developers-project-387/actions)

Разработайте совместно с ИИ сервис для бронирования календаря

Учебный проект Хекслета: https://ru.hexlet.io/programs/ai-for-developers
Как это должно работать: https://files.hexlet.app/a/2ipc5m

## Стек

- C# / ASP.NET Core 10 (MVC-контроллеры)
- React / TypeScript / Vite / Mantine
- TypeSpec / OpenAPI 3.0 для контракта API

## Установка

<!-- Опишите установку: клонирование, зависимости, переменные окружения -->

Для backend требуется .NET 10 SDK.
Для frontend — Node.js 22.20.0 и npm.

```bash
git clone https://github.com/gelatincrypto/ai-for-developers-project-387.git
cd ai-for-developers-project-387
npm --prefix frontend install
```

## Использование

<!-- Добавьте примеры запуска и запись asciinema — именно это смотрит работодатель -->

Из корня репозитория:

```bash
dotnet build Calendar.slnx
dotnet run --project src/Calendar.Api --launch-profile http
```

API доступен по адресу `http://localhost:5005`. Проверка работоспособности:

```bash
curl http://localhost:5005/api/health
```

Ожидаемый ответ: HTTP 200, `{"status":"ok"}`.

Для входа владельца задайте `Owner__Username` и `Owner__Password` как переменные
окружения **сервера** перед его запуском (в production — через менеджер секретов).
Без них вход запрещён; общих учётных данных в репозитории нет. Например, в
PowerShell перед `dotnet run`:

```powershell
$env:Owner__Username = 'ваш-логин'
$env:Owner__Password = 'ваш-секретный-пароль'
```

Страница `/owner` позволяет войти, создать тип события, посмотреть общий список
предстоящих встреч (время, тип и контакты гостя) и выйти. Контакты гостей
не доступны через публичные API. Запросы к закрытым API без входа
возвращают HTTP 401; публичный `/api/health` остаётся доступным. Для production
используйте HTTPS: сессионная cookie передаётся по защищённому соединению,
если запрос пришёл по HTTPS. Dev-proxy `/api` работает только в режиме разработки.

Контракт текущего API описан в `api-contract/main.tsp`. После установки
Node.js 22 и Java 21 подготовьте зависимости (один раз или после изменения lock-файлов):

```bash
npm --prefix api-contract ci
npm --prefix frontend ci
```

Затем вся цепочка генерации запускается **одной командой из корня репозитория**:

```bash
npm --prefix api-contract run generate:all
```

OpenAPI сохраняется в `api-contract/tsp-output/@typespec/openapi3/openapi.yaml`,
серверные модели и заготовки маршрутов — в `src/Calendar.Api/Generated/OpenApi/`,
клиентский SDK — в `frontend/src/generated/api/`. Все результаты хранятся в
репозитории; CI запускает ту же команду и проверяет, что они не устарели.
Она последовательно обновляет OpenAPI, серверные файлы и клиентский SDK.
Не редактируйте сгенерированные файлы вручную.

Серверные файлы генерирует OpenAPI Generator **7.21.0** (версия закреплена в
`api-contract/openapitools.json`), поэтому для локальной генерации необходима Java. В CI
используется Java 21. Генератор создаёт абстрактный контроллер, типы и
атрибут проверки модели; реальная логика находится в контроллерах
`src/Calendar.Api/Controllers/`. `[ApiController]` и
атрибуты моделей проверяют структурные правила входных данных.
Пока `GET /api/health` не принимает параметров или тела, поэтому проверять
на нём входящий запрос по модели нечего. Дополнительные бизнес-правила не
порождаются генератором и требуют отдельной реализации.
Контракт описывает проверку здоровья, вход владельца, типы событий, слоты и
бронирование. TypeSpec не генерирует бизнес-логику сервера.

Типы событий сохраняются в SQLite-файле `calendar.db` в каталоге API. Для
другого расположения (например, постоянного тома в production) задайте
`Calendar__DatabasePath` в конфигурации сервера; файл базы не коммитится.
В профиле владельца `Owner:TimeZone` по умолчанию равен `Europe/Moscow`; при
другом расположении владельца задайте `Owner__TimeZone` (идентификатор IANA).
Гость видит часы именно этого пояса. Слоты доступны по будням 09:00–18:00
на сегодня и следующие 13 дат с шагом начала 30 минут. Бронирования сохраняются
в той же SQLite-базе и занимают общий календарь независимо от типа события.

В другом терминале запустите frontend:

```bash
cd frontend
npm run dev
```

Откройте `http://localhost:5173` (точный адрес выводит Vite). На главной
странице (`/`) описан сервис, кнопка «Перейти к бронированию» ведёт на
`/booking`. Гость видит каталог типов, открывает календарь, вводит имя и email,
подтверждает выбранное время. При конфликте календарь обновляется. На этой странице
показана доступность backend: SDK запрашивает `/api/health` через dev-proxy Vite
на `http://localhost:5005`; для проверки статуса backend должен быть запущен.

Проверка production-сборки из папки `frontend`:

```bash
npm run build
```

Результат находится в `frontend/dist`. Dev-proxy не входит в production-сборку.
Корневой Dockerfile собирает frontend и backend вместе: ASP.NET Core обслуживает
`/api` и SPA на одном порту, включая прямые переходы на `/booking` и `/owner`.

## Docker и развёртывание

Из корня репозитория:

```bash
docker build -t calendar .
docker run --rm -p 8080:8080 -e PORT=8080 calendar
```

Откройте `http://localhost:8080/` или проверьте
`http://localhost:8080/api/health`. Контейнер слушает `0.0.0.0` на порту из
`PORT` (по умолчанию 8080). Если изменить `PORT`, измените и оба порта в
аргументе `-p`.

Для Render создайте Web Service из **этого** репозитория с Docker runtime и
health check path `/api/health`; стартовая команда уже задана в Dockerfile.
`Owner__Username` и `Owner__Password` задаются в настройках сервиса, не в образе.
SQLite хранится в `/data/calendar.db` (путь можно изменить через
`Calendar__DatabasePath`). Без постоянного диска данные исчезнут после
перезапуска или повторного развёртывания: для реальных бронирований нужен
постоянный том либо другое долговременное хранилище.

После публикации сервиса добавьте сюда проверенную ссылку на него; ссылка на
приложение предыдущего проекта не относится к этому репозиторию.

---

<details>
<summary>Автоматические тесты Хекслета</summary>

Тесты запускаются на каждый коммит. За запуск отвечает файл `.github/workflows/hexlet-check.yml` — не удаляйте и не переименовывайте ни его, ни репозиторий.

</details>

## Тесты backend

Из корня репозитория:

```bash
dotnet test Calendar.slnx
```

В `tests/Calendar.Api.Tests` находятся xUnit-тесты API: они запускают
приложение через `WebApplicationFactory<Program>` и проверяют health, вход
владельца, типы событий, слоты и бронирования. Отдельно запускать backend
или frontend не нужно.

Фронтенд-тесты страниц и работы с API запускаются командой
`npm --prefix frontend test`. Для них backend не нужен.

Зависимости backend восстанавливаются только из публичного nuget.org:
репозиторный `NuGet.Config` исключает унаследованные источники пакетов.
Для обычного восстановления выполните:

```bash
dotnet restore Calendar.slnx
```

Глобальная и пользовательская конфигурации NuGet не изменяются.

## Форматирование и статический анализ

Из корня репозитория:

```bash
dotnet format Calendar.slnx --verify-no-changes --no-restore
dotnet build Calendar.slnx -c Release -warnaserror --no-restore
dotnet test Calendar.slnx -c Release --no-build --no-restore
npm --prefix frontend run lint
npm --prefix frontend test
npm --prefix frontend run build
```

Перед проверками восстановите зависимости (`dotnet restore Calendar.slnx`
и `npm --prefix frontend install`). Чтобы применить форматирование C#, выполните
`dotnet format Calendar.slnx --no-restore`.

Правила форматирования заданы в `.editorconfig`, встроенные анализаторы .NET
включены в `Directory.Build.props`. Сгенерированные C#-файлы исключены из
анализа по стандартным суффиксам. Frontend использует Oxlint и Vitest; команда `build`
сначала проверяет TypeScript (`tsc -b`), затем собирает приложение Vite.

## CI

Workflow `.github/workflows/ci.yml` запускается на каждый push во все ветки
и при открытии или обновлении pull request. Три независимых задания проверяют:

- Контракт (Node.js 22, Java 21): установку зависимостей контракта и frontend,
  единую генерацию OpenAPI, серверных артефактов и SDK, проверку их актуальности.
- Backend (.NET 10): restore только из nuget.org через `NuGet.Config`,
  форматирование, Release-сборку с предупреждениями как ошибками и smoke-тест.
- Frontend (Node.js 22): `npm ci`, Oxlint, Vitest и production-сборку;
  кеш npm привязан к `frontend/package-lock.json`.

Workflow имеет только право чтения содержимого репозитория. Существующие
workflow Хекслета не изменяются; публикация и развертывание не выполняются.

## Релизы

Отдельный workflow `.github/workflows/release-please.yml` запускает
[Release Please Action v4](https://github.com/googleapis/release-please-action)
после push в `main`. Стратегия `simple` ведет **одну версию всего репозитория**
в заголовке release PR и общем `CHANGELOG.md`; после merge release PR создаются
тег и GitHub Release. Отдельного `version.txt` сейчас нет; версии C#
и `frontend/package.json` отдельно не обновляются. Публикации пакетов нет.

1. В GitHub откройте **Settings → Actions → General → Workflow permissions**
   и включите **Allow GitHub Actions to create and approve pull requests**.
   Workflow использует штатный `GITHUB_TOKEN`, без дополнительных секретов/PAT;
   права `contents`, `issues`, `pull-requests: write` нужны для релизов, меток и PR.
2. Используйте Conventional Commits: `feat: ...` — новая возможность (minor),
   `fix: ...` — исправление (patch), `feat!: ...` — несовместимое изменение (major).
   `chore: ...`, `docs: ...`, `ci: ...` допустимы, но сами по себе обычно
   не создают релиз. При squash merge проверьте **итоговое сообщение коммита**.
3. После merge `feat:`/`fix:` в `main` workflow создаст или обновит release PR
   с версией и changelog. Проверьте его; после merge release PR следующий
   запуск создаст тег и GitHub Release.

**Проверка release PR:** события, созданные стандартным `GITHUB_TOKEN`, могут
не запустить CI автоматически; отсутствие проверок не означает успех.
До merge явно проверьте актуальный head PR в отдельном чистом checkout:
`git fetch origin pull/NUMBER/head`, затем `git switch --detach FETCH_HEAD`
и `git rev-parse HEAD`. Сверьте SHA с head PR и выполните все шаги
`.github/workflows/ci.yml` для backend и frontend (включая restore и `npm ci`);
зафиксируйте SHA и результаты в PR. При обновлении head повторите проверку.
Это локальная проверка, а не замена обязательным GitHub checks: если branch
protection требует их, не обходите правило — нужен отдельно настроенный
разрешенный запуск CI для этого head. PAT для этого workflow не вводится.

Подробнее: [Release Please и Conventional Commits](https://github.com/googleapis/release-please).

## О Хекслете

[Хекслет](https://ru.hexlet.io/) — школа программирования: авторские программы обучения с практикой, поддержкой наставников и реальными проектами, которые остаются в резюме. Этот репозиторий — один из таких проектов.
