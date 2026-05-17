namespace Finance.Data.Banking;

using Finance.Core.Abstractions;
using Finance.Core.Banking;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public sealed class EfBankingQueries(FinanceDbContext dbContext, ITenantContext tenantContext) : IBankingQueries
{
    public async Task<IReadOnlyList<AccountDto>> GetAccounts(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await GetAccountDtos(tenantId, accountId: null, cancellationToken);
    }

    public async Task<AccountDto?> GetAccount(Guid accountId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return (await GetAccountDtos(tenantId, accountId, cancellationToken)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<AccountDto>> GetAccountDtos(Guid tenantId, Guid? accountId, CancellationToken cancellationToken)
    {
        var accounts = dbContext.BankAccounts.Where(x => x.TenantId == tenantId);

        if (accountId is { } requestedAccountId)
        {
            accounts = accounts.Where(x => x.Id == requestedAccountId);
        }

        var accountRows = await accounts
            .OrderBy(x => x.Name)
            .Select(x => new AccountRow(x.Id, x.BankConnectionId, x.Name, x.Currency))
            .ToListAsync(cancellationToken);

        var connectionIds = accountRows.Select(x => x.BankConnectionId).Distinct().ToList();
        var accountIds = accountRows.Select(x => x.Id).ToList();

        var institutions = await dbContext.BankConnections
            .Where(x => x.TenantId == tenantId && connectionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.InstitutionName })
            .ToDictionaryAsync(x => x.Id, x => x.InstitutionName, cancellationToken);

        var latestBalances = await dbContext.Balances
            .Where(x => x.TenantId == tenantId && accountIds.Contains(x.BankAccountId))
            .GroupBy(x => x.BankAccountId)
            .Select(x => new
            {
                AccountId = x.Key,
                CurrentMinorUnits = x.OrderByDescending(y => y.AsOf).Select(y => y.CurrentMinorUnits).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.AccountId, x => x.CurrentMinorUnits, cancellationToken);

        return accountRows
            .Select(x => new AccountDto(
                x.Id,
                x.Name,
                institutions.GetValueOrDefault(x.BankConnectionId, ""),
                x.Currency,
                latestBalances.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<BalanceDto>> GetBalances(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await dbContext.Balances
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.AsOf)
            .Select(x => new BalanceDto(x.BankAccountId, x.CurrentMinorUnits, x.Currency, x.AsOf))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetTransactions(TransactionQuery query, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var transactions = dbContext.BankTransactions.Where(x => x.TenantId == tenantId);

        if (query.AccountId is { } accountId)
        {
            transactions = transactions.Where(x => x.BankAccountId == accountId);
        }

        if (query.From is { } from)
        {
            transactions = transactions.Where(x => x.PostedDate >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(x => x.PostedDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            transactions = transactions.Where(x => EF.Functions.ILike(x.Description, $"%{query.Search}%"));
        }

        transactions = query.Sort switch
        {
            "amount" => transactions.OrderBy(x => x.AmountMinorUnits),
            "-amount" => transactions.OrderByDescending(x => x.AmountMinorUnits),
            "date" => transactions.OrderBy(x => x.PostedDate),
            _ => transactions.OrderByDescending(x => x.PostedDate)
        };

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        return await transactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionDto(x.Id, x.BankAccountId, x.Description, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImportRunDto>> GetImportRuns(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await dbContext.ImportRuns
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new ImportRunDto(x.Id, x.Source, x.Status, x.StartedAt, x.CompletedAt, x.ImportedCount, x.Error))
            .ToListAsync(cancellationToken);
    }

    private sealed record AccountRow(Guid Id, Guid BankConnectionId, string Name, string Currency);
}
