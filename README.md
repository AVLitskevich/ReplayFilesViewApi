# Веб-просмотрщик реплеев

Простой веб-сервис для просмотра и скачивания файлов реплеев из игры.

## Технологии

- ASP.NET Core 9.0 (Minimal API)
- Static Files (HTML + JavaScript)
- Docker

## Функциональность

- Просмотр списка доступных реплеев
- Скачивание файлов реплеев
- Автоматический парсинг даты из имени файла
- Защита от Path Traversal атак
- Валидация имён файлов

## Конфигурация

### appsettings.json

```json
{
  "ReplaySettings": {
    "FolderPath": "/home/fakemario/replays",
    "FileExtension": ".replay"
  }
}
```

### Переменные окружения

- `REPLAY_FOLDER_PATH` - переопределяет путь к папке с реплеями

## Запуск

### Локально

```bash
dotnet run
```

Приложение будет доступно по адресу: http://localhost:5000

### Docker

#### Сборка образа

```bash
docker build -t replay-viewer .
```

#### Запуск контейнера

```bash
docker run -d \
  -p 5050:8080 \
  -v /path/to/replays:/app/replays:ro \
  -e REPLAY_FOLDER_PATH=/app/replays \
  --name replay-viewer \
  replay-viewer
```

Где:
- `/path/to/replays` - путь к папке с реплеями на хосте
- `/app/replays` - путь к папке в контейнере
- `:ro` - монтирование в режиме read-only (для безопасности)

Приложение будет доступно по адресу: http://localhost:5050

## API Endpoints

### GET /

Отдаёт главную страницу с интерфейсом просмотра реплеев.

### GET /api/replays

Возвращает JSON-список файлов реплеев.

**Ответ:**

```json
{
  "replays": [
    {
      "fileName": "replay_2025-01-07_14-30-00.replay",
      "displayName": "Реплей 07.01.2025 14:30:00 UTC",
      "date": "2025-01-07T14:30:00",
      "sizeBytes": 102400
    }
  ]
}
```

### GET /api/replays/{fileName}

Скачивание файла реплея.

**Параметры:**
- `fileName` - имя файла реплея

**Ответ:**
- 200 OK - файл скачивается
- 404 Not Found - файл не найден или имя файла невалидно

## Формат имени файла реплея

`replay_YYYY-MM-DD_HH-mm-ss.replay`

Пример: `replay_2025-01-07_14-30-00.replay`

## Безопасность

- Валидация имени файла (только буквы, цифры, дефис, подчёркивание, точка)
- Защита от Path Traversal атак
- Проверка расширения файла
- Монтирование тома как read-only в Docker

## Разработка

### Структура проекта

```
ReplayFilesViewApi/
├── Models/                  # Модели данных
│   ├── ReplayFileInfo.cs
│   ├── ReplayListResponse.cs
│   └── ReplaySettings.cs
├── Services/                # Бизнес-логика
│   └── ReplayFileService.cs
├── wwwroot/                 # Статические файлы
│   └── index.html
├── Program.cs               # Точка входа и настройка API
├── appsettings.json         # Конфигурация
├── Dockerfile               # Docker-образ
└── README.md
```

### Тестирование

Для локального тестирования создайте папку `replays` в корне проекта и добавьте тестовые файлы:

```bash
mkdir replays
echo "Test data" > replays/replay_2025-01-07_14-30-00.replay
```

## Лицензия

MIT
