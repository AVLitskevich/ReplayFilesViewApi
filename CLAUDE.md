# ReplayFilesViewApi — Project Knowledge

## What This Project Does

A read-only web service that serves game replay files for multiple game projects. Each project has a name, description, a link to its WebGL build (hosted externally by nginx), and a replay file listing/download page. There is no authentication — all content is public. There are no write endpoints; users can only browse and download.

## Tech Stack

- **ASP.NET Core 9.0 Minimal API** with `WebApplication.CreateSlimBuilder`
- **AOT-ready**: `PublishAot=true`, `InvariantGlobalization=true` in csproj
- **Source-generated JSON**: `AppJsonSerializerContext` in Program.cs — every type returned from API must be registered here with `[JsonSerializable]`
- **No external NuGet packages** — only the base SDK
- **Vanilla HTML/JS frontend** in `wwwroot/` — no frameworks, no build step
- **Docker** multi-stage build, runs as non-root `appuser`

## Project Structure

```
Program.cs                          — App setup, DI, all route definitions
Models/
  ProjectSettings.cs                — Config model (Slug, Name, Description, WebGLUrl, ReplayFolderPath, FileExtension)
  ProjectInfo.cs                    — API response DTOs (ProjectInfo record, ProjectListResponse record)
  ReplayFileInfo.cs                 — Replay DTO (FileName, DisplayName, Date, SizeBytes) + ReplayListResponse
Services/
  ProjectService.cs                 — Reads project list from IOptions<List<ProjectSettings>>, lookup by slug
  ReplayFileService.cs              — File system operations: list replays, validate file paths
wwwroot/
  index.html                        — Main page: project cards grid (fetches /api/projects)
  replays.html                      — Replay list table for a project (fetches /api/projects/{slug}/replays)
Dockerfile                          — Multi-stage build, non-root user
docker-compose.yml                  — Production: mounts ~/replay-data/ for config and replay files
docker-compose.override.yml         — Local dev (gitignored): mounts ./replays/ and appsettings.Development.json
.github/workflows/deploy.yml        — GitHub Action: SCP + SSH deploy, docker compose up
appsettings.json                    — Production config template (example project entry)
appsettings.Development.json        — Local dev config with test projects (pixel-dash, dynamite-dude)
```

## API Endpoints

| Method | Route | Returns |
|--------|-------|---------|
| GET | `/api/projects` | `ProjectListResponse` — list of all projects |
| GET | `/api/projects/{slug}/replays` | `ReplayListResponse` — replay files for a project |
| GET | `/api/projects/{slug}/replays/{fileName}` | Binary file download (`application/octet-stream`) |
| GET | `/{slug}/replays` | Serves `replays.html` (the HTML page, not API) |
| GET | `/` | Serves `index.html` via static file middleware |

## Configuration

Projects are configured in `appsettings.json` under the `"Projects"` array:

```json
{
  "Projects": [
    {
      "Slug": "my-game",
      "Name": "My Game",
      "Description": "Game description",
      "WebGLUrl": "/webgl/my-game/index.html",
      "ReplayFolderPath": "/home/user/replays/my-game",
      "FileExtension": ".replay"
    }
  ]
}
```

- `Slug` — URL identifier, used in routes like `/{slug}/replays`
- `WebGLUrl` — link to externally hosted WebGL build (served by nginx, not this app)
- `ReplayFolderPath` — absolute path to the folder containing replay files on the host
- `FileExtension` — file extension filter (e.g. `.replay`)

## Security Measures

- **Path traversal protection** in `ReplayFileService.ValidateAndGetFilePath`:
  1. Regex whitelist: filename must match `^[a-zA-Z0-9_\-\.]+$`
  2. Extension check: filename must end with the configured `FileExtension`
  3. Path normalization: `Path.GetFullPath` + `StartsWith` check against base folder
- **Rate limiting**: 30 requests/minute per IP (fixed window), returns 429 on excess
- **Docker**: runs as non-root `appuser`
- **Read-only**: no upload/write/delete endpoints exist

## How the Frontend Works

Both HTML pages are vanilla JS with no dependencies:

- `index.html`: fetches `/api/projects`, renders a CSS grid of project cards. Each card has "Play" (links to `WebGLUrl`, opens in new tab) and "Replays" (links to `/{slug}/replays`) buttons.
- `replays.html`: extracts slug from URL path (`window.location.pathname.split('/')[1]`), fetches `/api/projects/{slug}/replays`, renders a table with download links pointing to `/api/projects/{slug}/replays/{fileName}`.

**Important**: `Results.File("replays.html", "text/html")` resolves paths relative to `wwwroot/`. Do NOT prepend `wwwroot/` — that would look for `wwwroot/wwwroot/replays.html`.

## Docker & Deployment

### Local development

```bash
docker compose up --build
# Uses docker-compose.yml + docker-compose.override.yml (auto-merged)
# Port: 5283, config: appsettings.Development.json, replays: ./replays/
```

Or without Docker: `dotnet run` (port 5283 via launchSettings).

### Production (server)

Code is deployed to `~/replay-viewer/`, data lives in `~/replay-data/`:

```
~/replay-data/
  appsettings.json          — server config (NOT overwritten by deploys)
  replays/
    my-game/                — replay files per project
    another-game/
```

`docker-compose.yml` mounts `~/replay-data/appsettings.json` and `~/replay-data/replays/` into the container.

### GitHub Action deploy flow

1. SCP copies repo to `~/replay-viewer/` (with `rm: true` — clean copy)
2. SSH creates `~/replay-data/replays/` if missing
3. SSH copies `appsettings.json` to `~/replay-data/` only on first deploy (`if [ ! -f ... ]`)
4. Runs `docker compose up -d --build`

Required GitHub secrets: `REMOTE_HOST`, `REMOTE_USER`, `REMOTE_PASSWORD`.

### Updating server config

```bash
nano ~/replay-data/appsettings.json
cd ~/replay-viewer && docker compose restart
```

No rebuild needed for config changes — just restart.

## Key Patterns to Follow

- **Adding a new API response type**: create a record in `Models/`, add `[JsonSerializable(typeof(YourType))]` to `AppJsonSerializerContext` in Program.cs — required for AOT serialization.
- **All services are singletons** — registered in Program.cs with `AddSingleton`.
- **Slug matching is case-insensitive** (`StringComparison.OrdinalIgnoreCase` in `ProjectService.GetBySlug`).
- **No OpenAPI/Swagger** — `WithOpenApi()` was removed because the `Microsoft.AspNetCore.OpenApi` package is not installed. Do not add it without also adding the NuGet package.
- **File names in replay listings** follow the pattern `replay_YYYY-MM-DD_HH-MM-SS.replay`. The date is parsed from the filename; if parsing fails, `File.GetLastWriteTimeUtc` is used as fallback.
- **`docker-compose.override.yml` is gitignored** — each developer creates their own for local paths.

## Known Limitations

- `launchSettings.json` has `launchUrl` pointing to `"todos"` (leftover from template) — cosmetic issue, does not affect functionality.
- No HTTPS at the app level — expected to sit behind nginx/reverse proxy with TLS termination.
- No caching headers on API responses or static files.
