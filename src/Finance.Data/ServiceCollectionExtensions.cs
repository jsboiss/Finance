namespace Finance.Data;

using Finance.Core.Abstractions;
using Finance.Core.Banking;
using Finance.Core.Redbark;
using Finance.Data.Banking;
using Finance.Data.Data;
using Finance.Data.Redbark;
using Finance.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FinanceDbContext>(x => x.UseNpgsql(GetPostgresConnectionString(configuration)));
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IBankingQueries, EfBankingQueries>();
        services.AddScoped<IRedbarkImportService, RedbarkImportService>();
        services.Configure<RedbarkOptions>(configuration.GetSection("Redbark"));
        services.AddHttpClient<IRedbarkClient, RedbarkClient>((provider, x) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedbarkOptions>>().Value;
            x.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddQuartz(x =>
        {
            var jobKey = new JobKey(nameof(RedbarkReconciliationJob));
            x.AddJob<RedbarkReconciliationJob>(y => y.WithIdentity(jobKey));
            x.AddTrigger(y => y.ForJob(jobKey).WithIdentity("redbark-reconciliation-daily").WithCronSchedule("0 0 2 * * ?"));
        });
        services.AddQuartzHostedService(x => x.WaitForJobsToComplete = true);
        return services;
    }

    private static string GetPostgresConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Finance");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return ConvertPostgresUrl(databaseUrl);
        }

        throw new InvalidOperationException("Configure ConnectionStrings:Finance or DATABASE_URL with a PostgreSQL connection string.");
    }

    private static string ConvertPostgresUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return databaseUrl;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? ""),
            Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "")
        };

        return builder.ConnectionString;
    }
}
