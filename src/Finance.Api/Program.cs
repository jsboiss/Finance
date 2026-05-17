using Finance.Api.Auth;
using Finance.Api.Data;
using Finance.Api.Endpoints;
using Finance.Core.Redbark;
using Finance.Data;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { })
    .AddScheme<AuthenticationSchemeOptions, DevDashboardAuthenticationHandler>(DevDashboardAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddFinanceData(builder.Configuration);
builder.Services.AddSingleton<RedbarkWebhookVerifier>();
builder.Services.AddCors(x => x.AddDefaultPolicy(y => y.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapDashboardEndpoints();
app.MapOperationsEndpoints();
app.MapExternalApiEndpoints();
app.MapRedbarkWebhookEndpoints();
app.MapDashboardStaticFiles();

await DevTenantSeeder.SeedDevTenant(app.Services);
await app.RunAsync();

public partial class Program;
