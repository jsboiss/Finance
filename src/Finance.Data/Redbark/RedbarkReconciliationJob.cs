namespace Finance.Data.Redbark;

using Finance.Core.Redbark;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;

public sealed class RedbarkReconciliationJob(FinanceDbContext dbContext, IRedbarkImportService importService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var tenantIds = await dbContext.Tenants.Select(x => x.Id).ToListAsync(context.CancellationToken);
        foreach (var tenantId in tenantIds)
        {
            await importService.ReconcileRecent(tenantId, context.CancellationToken);
        }
    }
}
