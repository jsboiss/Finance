# Finance

Local development uses Postgres in Docker, ASP.NET Core for the API, and Vite for the React dashboard.

## Start

From the repository root:

```powershell
docker compose up -d
dotnet run
```

In a second terminal:

```powershell
cd src/Finance.Web
npm run dev
```

Open:

```text
http://localhost:5173
```

The API listens on:

```text
http://localhost:5000
```

## Useful Commands

Run tests:

```powershell
dotnet test Finance.slnx
```

Run tests while the API is already running:

```powershell
dotnet test Finance.slnx -p:BaseOutputPath=.artifacts/test-bin/
```

Trigger imports:

```powershell
Invoke-RestMethod -Method Post http://localhost:5000/api/operations/backfill
Invoke-RestMethod -Method Post http://localhost:5000/api/operations/reconcile
Invoke-RestMethod -Method Post http://localhost:5000/api/operations/reconcile/full
```

Frontend production build:

```powershell
cd src/Finance.Web
npm run build
```
