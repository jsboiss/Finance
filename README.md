# Finance

Local development uses Postgres in Docker, ASP.NET Core for the API, and Vite for the React dashboard.

## Start

From the repository root:

```powershell
docker compose up -d
dotnet run --project src/Finance.Api
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

The dashboard uses the configured owner tenant:

```json
{
  "OwnerTenantId": "11111111-1111-1111-1111-111111111111",
  "OwnerTenantName": "Owner"
}
```

Startup creates this tenant if it does not exist. Dashboard endpoints are expected to operate only on this owner tenant's data.

## External API

External API routes require an API key in the `X-Api-Key` header.

```powershell
Invoke-RestMethod http://localhost:5000/api/v1/accounts -Headers @{ "X-Api-Key" = "<api-key>" }
```

Available routes:

```text
GET /api/v1/accounts
GET /api/v1/accounts/{accountId}
PUT /api/v1/accounts/{accountId}
GET /api/v1/balances
GET /api/v1/transactions?accountId=&from=&to=&search=&page=&pageSize=&sort=
GET /api/v1/tags
POST /api/v1/tags
DELETE /api/v1/tags/{tagId}
PUT /api/v1/transactions/{transactionId}/tags
GET /api/v1/merchant-tags
POST /api/v1/merchant-tags
DELETE /api/v1/merchant-tags/{ruleId}
GET /api/v1/imports
GET /api/v1/operations/status
GET /api/v1/subscriptions
GET /api/v1/subscriptions/{subscriptionId}
POST /api/v1/subscriptions
PUT /api/v1/subscriptions/{subscriptionId}
DELETE /api/v1/subscriptions/{subscriptionId}
GET /api/v1/subscription-suggestions
POST /api/v1/subscription-suggestions/refresh
POST /api/v1/subscription-suggestions/{suggestionId}/accept
POST /api/v1/subscription-suggestions/{suggestionId}/dismiss
```

Transaction `sort` supports `date`, `-date`, `amount`, and `-amount`. Dates use `yyyy-MM-dd`.
Bank transaction records are not creatable through the external API; tenant transaction data is synced by the backend.

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
