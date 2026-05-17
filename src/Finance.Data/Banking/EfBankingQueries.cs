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
            .Select(x => new AccountRow(x.Id, x.BankConnectionId, x.Name, x.AccountNumber, x.Currency))
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
                x.AccountNumber,
                GetAccountDisplayName(x.Name, x.AccountNumber),
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
        var transactionRows = await transactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionRow(
                x.Id,
                x.BankAccountId,
                x.ExternalTransactionId,
                x.ExternalAccountName,
                x.Description,
                x.MerchantName,
                x.MerchantCategoryCode,
                x.Category,
                x.AmountMinorUnits,
                x.Currency,
                x.PostedDate,
                x.PostedAt,
                x.Direction,
                x.Status))
            .ToListAsync(cancellationToken);

        var accountIds = transactionRows.Select(x => x.AccountId).Distinct().ToList();
        var accountDisplays = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && accountIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.AccountNumber })
            .ToDictionaryAsync(x => x.Id, x => new AccountDisplay(x.Name, x.AccountNumber), cancellationToken);

        var transactionIds = transactionRows.Select(x => x.Id).ToList();
        var tagsByTransaction = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BankTransactionId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BankTransactionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);

        return transactionRows
            .Select(x =>
            {
                var account = accountDisplays.GetValueOrDefault(x.AccountId, new AccountDisplay(x.ExternalAccountName, ""));
                return new TransactionDto(
                    x.Id,
                    x.AccountId,
                    x.ExternalTransactionId,
                    account.Name,
                    account.AccountNumber,
                    GetAccountDisplayName(account.Name, account.AccountNumber),
                    x.Description,
                    x.MerchantName,
                    x.MerchantCategoryCode,
                    x.Category,
                    x.AmountMinorUnits,
                    x.Currency,
                    x.PostedDate,
                    x.PostedAt,
                    x.Direction,
                    x.Status,
                    tagsByTransaction.GetValueOrDefault(x.Id, []));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TransactionTagDto>> GetTags(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await dbContext.TransactionTags
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new TransactionTagDto(x.Id, x.Name, x.Color))
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionTagDto> CreateTag(CreateTransactionTagRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Tag name is required.");
        }

        var tag = await dbContext.TransactionTags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, cancellationToken);
        if (tag is null)
        {
            tag = new TransactionTag { TenantId = tenantId, Name = name };
            dbContext.TransactionTags.Add(tag);
        }

        tag.Color = string.IsNullOrWhiteSpace(request.Color) ? tag.Color : request.Color.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TransactionTagDto(tag.Id, tag.Name, tag.Color);
    }

    public async Task<bool> DeleteTag(Guid tagId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var exists = await dbContext.TransactionTags.AnyAsync(x => x.TenantId == tenantId && x.Id == tagId, cancellationToken);
        if (!exists)
        {
            return false;
        }

        await dbContext.BankTransactionTags.Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.MerchantTags.Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.TransactionTags.Where(x => x.TenantId == tenantId && x.Id == tagId).ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TransactionTagDto>> SetTransactionTags(Guid transactionId, UpdateTransactionTagsRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var exists = await dbContext.BankTransactions.AnyAsync(x => x.TenantId == tenantId && x.Id == transactionId, cancellationToken);
        if (!exists)
        {
            return [];
        }

        var requestedTagIds = request.TagIds.Distinct().ToList();
        var tags = await dbContext.TransactionTags
            .Where(x => x.TenantId == tenantId && requestedTagIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var validTagIds = tags.Select(x => x.Id).ToHashSet();

        await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.BankTransactionId == transactionId && !validTagIds.Contains(x.TransactionTagId))
            .ExecuteDeleteAsync(cancellationToken);

        var existingTagIds = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.BankTransactionId == transactionId)
            .Select(x => x.TransactionTagId)
            .ToListAsync(cancellationToken);

        foreach (var tagId in validTagIds.Except(existingTagIds))
        {
            dbContext.BankTransactionTags.Add(new BankTransactionTag { TenantId = tenantId, BankTransactionId = transactionId, TransactionTagId = tagId, Source = "manual" });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return tags.Select(x => new TransactionTagDto(x.Id, x.Name, x.Color)).OrderBy(x => x.Name).ToList();
    }

    public async Task<IReadOnlyList<MerchantTagRuleDto>> GetMerchantTagRules(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await dbContext.MerchantTags
            .Where(x => x.TenantId == tenantId)
            .Join(dbContext.TransactionTags.Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { Rule = x, Tag = y })
            .OrderBy(x => x.Rule.MerchantName)
            .ThenBy(x => x.Tag.Name)
            .Select(x => new MerchantTagRuleDto(x.Rule.Id, x.Rule.MerchantName, new TransactionTagDto(x.Tag.Id, x.Tag.Name, x.Tag.Color)))
            .ToListAsync(cancellationToken);
    }

    public async Task<MerchantTagRuleDto> CreateMerchantTagRule(CreateMerchantTagRuleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var merchantName = request.MerchantName.Trim();
        if (string.IsNullOrWhiteSpace(merchantName))
        {
            throw new InvalidOperationException("Merchant name is required.");
        }

        var merchantKey = GetMerchantKey(merchantName);
        var tag = await dbContext.TransactionTags.FirstAsync(x => x.TenantId == tenantId && x.Id == request.TagId, cancellationToken);
        var rule = await dbContext.MerchantTags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.MerchantKey == merchantKey && x.TransactionTagId == request.TagId, cancellationToken);
        if (rule is null)
        {
            rule = new MerchantTag { TenantId = tenantId, MerchantName = merchantName, MerchantKey = merchantKey, TransactionTagId = request.TagId };
            dbContext.MerchantTags.Add(rule);
        }

        await ApplyMerchantTag(tenantId, merchantKey, request.TagId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MerchantTagRuleDto(rule.Id, rule.MerchantName, new TransactionTagDto(tag.Id, tag.Name, tag.Color));
    }

    public async Task<bool> DeleteMerchantTagRule(Guid ruleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        return await dbContext.MerchantTags.Where(x => x.TenantId == tenantId && x.Id == ruleId).ExecuteDeleteAsync(cancellationToken) > 0;
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

    public async Task<OperationsStatusDto> GetOperationsStatus(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var now = DateTimeOffset.UtcNow;
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var requests = dbContext.RedbarkRequestLogs.Where(x => x.TenantId == tenantId);
        var todayCount = await requests.CountAsync(x => x.RequestedAt >= today, cancellationToken);
        var monthCount = await requests.CountAsync(x => x.RequestedAt >= month, cancellationToken);
        var totalCount = await requests.CountAsync(cancellationToken);
        var lastRequestAt = await requests
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => (DateTimeOffset?)x.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new OperationsStatusDto(todayCount, monthCount, totalCount, lastRequestAt);
    }

    private static string GetAccountDisplayName(string name, string accountNumber)
    {
        return string.IsNullOrWhiteSpace(accountNumber) ? name : $"{name} - {accountNumber}";
    }

    private async Task ApplyMerchantTag(Guid tenantId, string merchantKey, Guid tagId, CancellationToken cancellationToken)
    {
        var transactionIds = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.MerchantName != null && x.MerchantName.ToLower() == merchantKey)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var existingTransactionIds = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId && transactionIds.Contains(x.BankTransactionId))
            .Select(x => x.BankTransactionId)
            .ToListAsync(cancellationToken);

        foreach (var transactionId in transactionIds.Except(existingTransactionIds))
        {
            dbContext.BankTransactionTags.Add(new BankTransactionTag { TenantId = tenantId, BankTransactionId = transactionId, TransactionTagId = tagId, Source = "merchant" });
        }
    }

    private static string GetMerchantKey(string merchantName)
    {
        return merchantName.Trim().ToLowerInvariant();
    }

    private sealed record AccountDisplay(string Name, string AccountNumber);

    private sealed record AccountRow(Guid Id, Guid BankConnectionId, string Name, string AccountNumber, string Currency);

    private sealed record TransactionRow(
        Guid Id,
        Guid AccountId,
        string ExternalTransactionId,
        string ExternalAccountName,
        string Description,
        string? MerchantName,
        string? MerchantCategoryCode,
        string Category,
        long AmountMinorUnits,
        string Currency,
        DateOnly PostedDate,
        DateTimeOffset? PostedAt,
        string Direction,
        string Status);
}
