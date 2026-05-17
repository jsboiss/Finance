# External Agent Setup

This guide is for an agent or developer building their own UI against Finance.

## Connection Model

Finance owns bank-data ingestion. External clients should not create bank connections, bank accounts, balances, or bank transaction records directly.

The backend syncs tenant banking data from Redbark and creates:

- bank connections
- bank accounts
- balances
- posted transactions

External clients use the API to read that synced data and manage user-owned metadata such as custom account names, tags, merchant tag rules, and subscriptions.

## Required Inputs

Ask the Finance operator for:

- `baseUrl`, for example `http://localhost:5000`
- `apiKey`

Every external request must include:

```http
X-Api-Key: <apiKey>
```

## Access Model

The Finance operator is the only person who can use the dashboard. External developers do not need dashboard access, repository access, or database access.

The operator will give you:

- the API base URL
- an API key

Do not ask for, hard-code, or send a tenant id. Tenant selection is not part of the external API contract.

When you send `X-Api-Key`, the backend looks up the API client, resolves the tenant associated with that key, and filters all reads and writes to that tenant. If you need access to a different tenant, the operator must issue a different API key scoped to that tenant.

Bank data sync is also operator-owned. If accounts or transactions are missing, ask the operator to run a sync.

## Agent Bootstrap Flow

1. Verify authentication:

```powershell
$headers = @{ "X-Api-Key" = "<apiKey>" }
Invoke-RestMethod "$baseUrl/api/v1/accounts" -Headers $headers
```

2. Read synced accounts:

```http
GET /api/v1/accounts
```

3. Set a custom account name:

```http
PUT /api/v1/accounts/{accountId}
Content-Type: application/json

{
  "customName": "Bills account"
}
```

4. Read transactions:

```http
GET /api/v1/transactions?page=1&pageSize=100&sort=-date
GET /api/v1/transactions?accountId={accountId}&from=2026-01-01&to=2026-05-31
```

5. Create and assign tags:

```http
POST /api/v1/tags
Content-Type: application/json

{
  "name": "Tax",
  "color": "#0f766e"
}
```

```http
PUT /api/v1/transactions/{transactionId}/tags
Content-Type: application/json

{
  "tagIds": ["{tagId}"]
}
```

## Writable Resources

External clients can write:

- account custom names
- transaction tags
- merchant tag rules
- subscriptions
- subscription suggestion actions

External clients cannot write:

- bank connections
- bank accounts
- balances
- bank transaction records

Those records are owned by the backend sync pipeline.
