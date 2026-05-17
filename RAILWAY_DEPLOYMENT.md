# Railway Deployment

This app deploys to Railway as one web service that serves both the ASP.NET Core API and the built React dashboard. Railway Postgres is the production database.

## One-time Railway setup

1. Create a Railway project from the GitHub repo.
2. Select the `main` branch.
3. Let Railway build from the root `Dockerfile`.
4. Add a PostgreSQL service in the same Railway project.
5. Set the app service variables below.

## Required variables

Set these on the Railway app service:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Finance=<Railway internal Postgres connection string>
OwnerTenantId=<stable GUID for your owner tenant>
OwnerTenantName=Owner
OwnerDashboardUsername=<your dashboard username>
OwnerDashboardPassword=<strong dashboard password>
Redbark__BaseUrl=<real Redbark API base URL>
Redbark__ApiKey=<real Redbark API key>
Redbark__WebhookSecret=<real Redbark webhook secret>
```

Use the Railway internal Postgres URL for `ConnectionStrings__Finance` so traffic stays inside Railway.

## First deploy

1. Push the deployment changes to GitHub `main`.
2. Railway will build and publish automatically.
3. Open the generated Railway domain.
4. Sign in with `OwnerDashboardUsername` and `OwnerDashboardPassword`.
5. Confirm the owner tenant appears on the Settings page.
6. Run a Redbark backfill or reconcile from the dashboard.

On startup, the API runs EF Core migrations and seeds the owner tenant/default tags.

## External API setup

1. In the dashboard Settings page, create an external tenant if one does not already exist.
2. Generate an API key for that external tenant.
3. Give the external developer only:
   - the Railway base URL
   - their API key
   - `EXTERNAL_AGENT_SETUP.md`

External API keys are scoped to their tenant. They should not send a tenant ID in requests.

## Smoke tests

After deploy:

```powershell
# Dashboard should require Basic auth.
Invoke-WebRequest https://<railway-domain>/ -SkipHttpErrorCheck

# External API should reject missing API key.
Invoke-WebRequest https://<railway-domain>/api/v1/accounts -SkipHttpErrorCheck

# External API should accept a valid tenant-scoped key.
Invoke-RestMethod https://<railway-domain>/api/v1/accounts -Headers @{ 'X-Api-Key' = '<api-key>' }
```
