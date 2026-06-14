namespace Finance.Data.Banking;

using System.Text.RegularExpressions;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public static class DefaultBankingData
{
    public static string InternalTransferTagName => "Internal";

    public static string InternalTransferTagColor => "#64748b";

    public static IReadOnlyList<string> InternalTransferMerchantNames { get; } =
    [
        "Internal transfer",
        "Transfer",
        "Bank transfer",
        "Funds transfer",
        "Account transfer"
    ];

    public static async Task<TransactionTag> EnsureDefaultTags(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tag = await dbContext.TransactionTags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == InternalTransferTagName, cancellationToken);
        if (tag is null)
        {
            tag = new TransactionTag { TenantId = tenantId, Name = InternalTransferTagName, Color = InternalTransferTagColor };
            dbContext.TransactionTags.Add(tag);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureInternalTransferRules(tenantId, tag.Id, dbContext, cancellationToken);
        await ApplyInternalTransferTag(tenantId, tag.Id, dbContext, cancellationToken);
        return tag;
    }

    public static string GetMerchantKey(string merchantName)
    {
        var normalizedValue = Regex.Replace(merchantName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        var ignoredTokens = new HashSet<string> { "au", "aus", "vi", "pty", "ltd", "limited", "australia", "melbourne", "sydney", "brisbane", "card", "com" };
        return string.Join(" ", normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => !ignoredTokens.Contains(x)));
    }

    public static bool MatchesMerchantRule(string transactionMerchantKey, string ruleMerchantKey)
    {
        return transactionMerchantKey == ruleMerchantKey
            || transactionMerchantKey.StartsWith($"{ruleMerchantKey} ", StringComparison.Ordinal);
    }

    private static async Task EnsureInternalTransferRules(Guid tenantId, Guid tagId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var merchantKeys = InternalTransferMerchantNames.Select(GetMerchantKey).ToHashSet();
        var existingMerchantKeys = await dbContext.MerchantTags
            .Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId && merchantKeys.Contains(x.MerchantKey))
            .Select(x => x.MerchantKey)
            .ToListAsync(cancellationToken);

        foreach (var name in InternalTransferMerchantNames)
        {
            var merchantKey = GetMerchantKey(name);
            if (!existingMerchantKeys.Contains(merchantKey))
            {
                dbContext.MerchantTags.Add(new MerchantTag { TenantId = tenantId, MerchantName = name, MerchantKey = merchantKey, TransactionTagId = tagId });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyInternalTransferTag(Guid tenantId, Guid tagId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var merchantKeys = InternalTransferMerchantNames.Select(GetMerchantKey).ToHashSet();
        var transactionRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.MerchantName, x.Description })
            .ToListAsync(cancellationToken);
        var transactionIds = transactionRows
            .Where(x => MatchesInternalTransfer(x.MerchantName, x.Description, merchantKeys))
            .Select(x => x.Id)
            .ToList();

        if (transactionIds.Count == 0)
        {
            return;
        }

        var existingTransactionIds = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId && transactionIds.Contains(x.BankTransactionId))
            .Select(x => x.BankTransactionId)
            .ToListAsync(cancellationToken);

        foreach (var transactionId in transactionIds.Except(existingTransactionIds))
        {
            dbContext.BankTransactionTags.Add(new BankTransactionTag { TenantId = tenantId, BankTransactionId = transactionId, TransactionTagId = tagId, Source = "default" });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool MatchesInternalTransfer(string? merchantName, string description, HashSet<string> merchantKeys)
    {
        if (!string.IsNullOrWhiteSpace(merchantName) && MatchesMerchantKey(GetMerchantKey(merchantName), merchantKeys))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(description) && MatchesMerchantKey(GetMerchantKey(description), merchantKeys);
    }

    private static bool MatchesMerchantKey(string merchantKey, HashSet<string> merchantKeys)
    {
        return merchantKeys.Any(x => MatchesMerchantRule(merchantKey, x));
    }
}
