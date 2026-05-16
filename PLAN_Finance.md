# Banking Backend + Dashboard Project Plan

## Summary

Create a greenfield .NET 10 banking ingestion and dashboard application in `C:\Users\Joshua\Documents\Programming\Finance`.

The system will ingest posted banking transactions and balances from Redbark into Postgres, support multiple tenants, expose tenant-filtered APIs for external consumers, and include its own React dashboard. Development will use separate backend and Vite dev servers for fast iteration, while production will serve the built React app from ASP.NET Core.

API documentation UI is intentionally out of scope for v1. The backend can still emit OpenAPI metadata later for Orval and future docs.

## Key Architecture

- Solution format:
  - Use a `.slnx` solution targeting **.NET 10**.
  - Create separate projects for API, application logic, infrastructure, and tests.

- Backend stack:
  - ASP.NET Core on .NET 10.
  - Postgres for persistence.
  - EF Core for schema, migrations, normal reads/writes, and tenant-filtered queries.
  - Quartz.NET for scheduled backfill and reconciliation jobs.
  - Redbark ingestion module with typed HTTP client, webhook verification, idempotent imports, and reconciliation jobs.
  - OpenAPI generation enabled for client generation only, without serving a docs UI in v1.

- Frontend stack:
  - React + TypeScript.
  - Vite for development and production bundling.
  - TanStack Router for routing.
  - TanStack Query for API/server state.
  - TanStack Table for transaction/account grids.
  - Tailwind CSS for styling.
  - shadcn/ui for reusable components.
  - Orval for generating the TypeScript API client from the ASP.NET OpenAPI document.

- Development workflow:
  - Backend runs separately, for example `https://localhost:5001`.
  - Frontend runs separately through Vite, for example `http://localhost:5173`.
  - Vite proxies `/api` to the ASP.NET backend during development.
  - No manual `npm run build` is needed during normal frontend development.
  - Production builds React into ASP.NET static assets and serves it from the API app.

## Implementation Changes

- Project layout:
  - `src/Finance.Api`: ASP.NET Core host, endpoints, auth, static React hosting, Quartz registration.
  - `src/Finance.Application`: use cases, interfaces, tenancy abstractions, banking query services, ingestion orchestration.
  - `src/Finance.Infrastructure`: EF Core DbContext, migrations, repositories, Redbark client, Quartz jobs.
  - `src/Finance.Web`: React/Vite dashboard.
  - `tests/Finance.Tests`: unit tests.
  - `tests/Finance.IntegrationTests`: Postgres-backed integration tests using Testcontainers.
  - `docker-compose.yml`: local Postgres.

- Data model:
  - Add tenant-owned banking tables for tenants, users, tenant memberships, API clients, bank connections, bank accounts, balances, transactions, webhook events, and import runs.
  - Store all monetary values as integer minor units, such as cents.
  - Store Redbark raw JSON on imported entities for audit/debugging.
  - Use tenant-safe unique constraints such as `tenant_id + external_transaction_id`.
  - Exclude pending transactions in v1.

- Tenancy and auth:
  - Dashboard users authenticate through ASP.NET Core auth.
  - External consumers authenticate using hashed, tenant-scoped API keys.
  - Resolve `tenantId` from the authenticated principal or API key.
  - Do not accept arbitrary `tenantId` from normal request bodies or query strings.
  - Apply tenant filtering centrally through application services and/or EF query patterns.

- Backend APIs:
  - Dashboard APIs under `/api`.
  - External consumer APIs under `/api/v1`.
  - Redbark webhook endpoint at `/webhooks/redbark`.
  - Manual operational endpoints for backfill/reconciliation can be available to authenticated dashboard users.
  - Generate an OpenAPI document for Orval, but do not add Scalar/Swagger UI in v1.

- Redbark ingestion:
  - Implement typed Redbark REST client for connections, accounts, balances, and paginated transactions.
  - Verify webhook HMAC signature against the raw request body before parsing JSON.
  - Store webhook events durably with duplicate protection.
  - Use a shared import flow for webhook events, backfill, and reconciliation.
  - Use idempotent upserts for connections, accounts, balances, and transactions.
  - Initial backfill imports the last 24 months.
  - Daily reconciliation imports the last 60 days.
  - Quartz schedules reconciliation; manual runs are supported through backend endpoints.

- Frontend:
  - Create a dashboard shell with authenticated routes.
  - Add account list/detail views.
  - Add transaction table with filtering, sorting, pagination, and account/date filters.
  - Add balance overview.
  - Add import/reconciliation status view.
  - Generate the API client with Orval from the backend OpenAPI document.
  - Use TanStack Query hooks around generated API calls.

- Production hosting:
  - Vite production build outputs static files for ASP.NET Core to serve.
  - ASP.NET Core serves `index.html` fallback for dashboard routes.
  - API routes and webhook routes remain handled by ASP.NET Core.
  - Production deployment is a single backend service plus Postgres.

## Test Plan

- Unit tests:
  - Redbark webhook signature verification accepts valid payloads and rejects tampered payloads.
  - Pending transactions are skipped.
  - Amount values preserve sign and integer minor units.
  - Tenant resolution rejects unauthenticated or invalid API key requests.
  - API key hashing/verification works without storing plaintext tokens.

- Integration tests:
  - EF migrations create a clean Postgres schema.
  - Backfill imports paginated transactions across multiple accounts.
  - Webhook import and reconciliation produce one row for the same Redbark transaction.
  - Tenant A cannot read Tenant B accounts, balances, transactions, import runs, or API data.
  - Unique constraints prevent duplicate connections/accounts/transactions per tenant.
  - External `/api/v1` endpoints return only the API key tenant’s data.

- Frontend checks:
  - Vite dev server proxies API requests correctly.
  - Orval generates a working TypeScript client from the backend OpenAPI output.
  - Transaction table supports sorting, filtering, pagination, and loading/error states.
  - Production React build is served correctly by ASP.NET Core with route fallback.

- Manual acceptance:
  - Start Postgres with Docker Compose.
  - Run backend and frontend dev servers separately.
  - Complete a local backfill.
  - Receive a Redbark webhook through a tunnel.
  - Confirm posted transactions and balances appear in the dashboard.
  - Confirm an external API key can fetch only its tenant’s accounts and transactions.

## Assumptions

- This is a greenfield repo; no existing source files need to be preserved.
- V1 supports posted banking data only; pending transactions are excluded.
- Redbark is the initial banking provider.
- Postgres is the only database target for v1.
- Tenant isolation is required from the first implementation.
- Dashboard auth plus tenant-scoped API keys is the v1 auth model.
- React is served by Vite in development and by ASP.NET Core static files in production.
- API docs UI is not included in v1, but OpenAPI output remains available for Orval client generation.
