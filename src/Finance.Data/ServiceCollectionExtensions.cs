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
using Quartz;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FinanceDbContext>(x => x.UseNpgsql(configuration.GetConnectionString("Finance")));
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
}
