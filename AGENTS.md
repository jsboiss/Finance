## C# style

Avoid using fields, and instead use properties where stored state is necessary. Otherwise, use primary constructor params directly.

When using LINQ queries, use `x`, `y`, `z`, `a`, `b`, `c` for each lambda level instead of words or random letters.

Except for when doing a `foreach (var x in y)`, `x` should always be the non-pluralized version of `y`, like:
foreach (var key in keys) { }

Do not name methods with the suffix Async for async methods.

Do not use if/foreach/while statements without braces { }.

## Tenant scoping

Dashboard endpoints must only show and mutate the owner tenant's data.

Any dashboard endpoint that reads or writes tenant-owned rows must get the tenant from `ITenantContext.TenantId`. Do not parse tenant ids from route values, request bodies, query strings, or arbitrary claims for dashboard data access.

When querying `FinanceDbContext` directly from a dashboard endpoint, every tenant-owned entity query must include `x.TenantId == tenantContext.TenantId` before materializing, updating, or deleting records. Prefer existing query/service methods that already use `ITenantContext`.

External API endpoints are scoped by `X-Api-Key`; do not let an external API request choose a tenant id directly.

The dashboard may include explicit operator provisioning screens for tenants and API clients. Those screens can create tenants and issue/revoke API clients for selected tenants, but they must not expose other tenants' banking data in dashboard views.

Do not add a dashboard tenant selector for banking data unless the product intentionally becomes an admin multi-tenant banking dashboard.

## Git

Do not push changes to any remote unless the user explicitly asks for a push.

## Builds

Don't run a build on every single small change, especially simple CSS/styling changes.
