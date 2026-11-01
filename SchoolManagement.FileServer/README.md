# SchoolManagement.FileServer

Step 2 of the School Management System — a **separate .NET 9 Web API** that
handles file uploads/downloads for the Main API. It writes file metadata into
the **same `SchoolDB.FileStore` table** the Main API already migrated, so the
Main API only needs to remember a `FileId`.

## Architecture at a glance

```
Angular ──upload──► FileServer ──(insert row)──► SchoolDB.FileStore
                       │                            ▲
                       └── saves bytes to ────► Storage/<bucket>/<yyyy>/<MM>/<guid>.<ext>
                                                    │
Main API ──"please remember FileId 42"◄─────────────┘
```

- **FileServer never modifies migrations.** The Main API owns the `FileStore`
  schema (its migration already exists). FileServer just inserts/reads rows.
- **Storage layout** (improvement over the doc's flat `images/documents/homework/`
  folders): `Storage/<bucket>/<yyyy>/<MM>/<guid><ext>` — keeps folder sizes
  bounded over time.
- **Auth is dual** — JWT (same key as Main API) for browser-direct uploads,
  plus `X-Api-Key` for server-to-server calls from the Main API.

## Endpoints

| Verb | Route | Auth | Purpose |
|------|-------|------|---------|
| POST   | `/api/files/upload`         | JWT or ApiKey                | Upload a file. Form fields: `file` (required), `entityType` (required), `entityId` (optional). Returns `{ fileId, fileUrl, originalName, fileType, sizeBytes }`. |
| GET    | `/api/files/{fileId}`       | JWT or ApiKey                | Stream/download the file. Supports HTTP range requests. |
| GET    | `/api/files/{fileId}/info`  | JWT or ApiKey                | File metadata (no disk path leaked). |
| DELETE | `/api/files/{fileId}`       | JWT (SuperAdmin/Admin) or ApiKey | Soft-delete (sets `IsDeleted = 1`). Physical file is kept. |
| GET    | `/health`                   | anonymous                    | Liveness ping. |

### `entityType` values accepted

`image`, `student-photo`, `document`, `birth-certificate`, `prev-school-cert`,
`homework`, `homework-submission`.

Per-type extension allow-lists and size limits live in `appsettings.json` under
`FileServer:AllowedExtensions` and `FileServer:MaxSizeBytesPerEntity`.

## Running it

The database, JWT key, and ApiKey are pre-configured to match the Main API
(`SchoolManagement.API/appsettings.json`).

```powershell
cd "D:\School System\SchoolManagement.FileServer"
dotnet restore
dotnet run
```

Swagger UI: <http://localhost:5002/swagger>
Health:      <http://localhost:5002/health>

> **Migrations.** This project does **not** create migrations. The Main API
> already migrated the `FileStore` table. Just make sure you've run the Main
> API's `dotnet ef database update` once.

## Calling FileServer from the Main API

```csharp
// In a Main API service:
using var http   = httpClientFactory.CreateClient();
http.BaseAddress = new Uri(_config["FileServer:BaseUrl"]!);
http.DefaultRequestHeaders.Add("X-Api-Key", _config["FileServer:ApiKey"]!);

using var form = new MultipartFormDataContent();
form.Add(new StreamContent(stream), "file", originalName);
form.Add(new StringContent("student-photo"), "entityType");
form.Add(new StringContent(studentId.ToString()), "entityId");

var resp = await http.PostAsync("/api/files/upload", form);
resp.EnsureSuccessStatusCode();
var payload = await resp.Content.ReadFromJsonAsync<FileUploadResponse>();
// store payload.FileId on the Student row
```

## Calling FileServer from Angular

```ts
// Angular FileUploadService
const form = new FormData();
form.append('file', selectedFile);
form.append('entityType', 'student-photo');
form.append('entityId', String(studentId));

this.http.post<FileUploadResponse>(
  `${environment.fileServerUrl}/api/files/upload`,
  form,
  { headers: { Authorization: `Bearer ${this.auth.token}` } }
);
```

Use the returned `fileUrl` directly in `<img [src]="...">` or download links.

## Project layout

```
SchoolManagement.FileServer/
├── Auth/                  ApiKey authentication handler
├── Configuration/         FileServerOptions (typed appsettings)
├── Controllers/           FilesController — the 4 endpoints
├── Data/                  FileServerDbContext + FileStoreEntity (shared table)
├── Models/                Request/response DTOs + EntityTypes catalog
├── Services/              FileStorageService (disk I/O + validation)
├── Storage/               Disk-backed uploads (kept; contents gitignored)
│   ├── images/
│   ├── documents/
│   └── homework/
├── Properties/launchSettings.json
├── appsettings.json
├── appsettings.Development.json
└── Program.cs
```

## What lines up with the task doc

- [x] Separate .NET 9 Web API project (`SchoolManagement.FileServer`)
- [x] Returns `FileId` to the Main API after upload
- [x] Storage folders `images/`, `documents/`, `homework/` (plus `<yyyy>/<MM>` subfolders — improvement)
- [x] Endpoints: upload, download, info, delete
- [x] Delete restricted to SuperAdmin/Admin
- [x] FileStore row written in the main DB (shared `SchoolDB`)
- [x] Per-entity-type allow-list + size cap (improvement)
- [x] Soft delete only — physical file retained for forensics

## Not done in Step 2 (future steps)

- Hangfire/background job to purge soft-deleted files older than N days
- Virus scan integration (ClamAV) — listed as an "Improvement Suggestion" only
- Signed expiring URLs for sensitive documents
