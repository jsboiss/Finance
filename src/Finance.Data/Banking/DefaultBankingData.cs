namespace Finance.Data.Banking;

using System.Text.RegularExpressions;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public static class DefaultBankingData
{
    public static string InternalTransferTagName => "Internal";

    public static string InternalTransferTagColor => "#64748b";

    public static int InternalTransferPostingWindowDays => 3;

    public static async Task<TransactionTag> EnsureDefaultTags(Guid tenantId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var tag = await dbContext.TransactionTags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == InternalTransferTagName, cancellationToken);
        if (tag is null)
        {
            tag = new TransactionTag { TenantId = tenantId, Name = InternalTransferTagName, Color = InternalTransferTagColor };
            dbContext.TransactionTags.Add(tag);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RemoveInternalTransferRules(tenantId, tag.Id, dbContext, cancellationToken);
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

    public static bool IsInternalTransferTag(string tagName)
    {
        return tagName.Equals(InternalTransferTagName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RemoveInternalTransferRules(Guid tenantId, Guid tagId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.MerchantTags
            .Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task ApplyInternalTransferTag(Guid tenantId, Guid tagId, FinanceDbContext dbContext, CancellationToken cancellationToken)
    {
        var transactionRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.Status == "posted" && x.AmountMinorUnits != 0)
            .Select(x => new InternalTransferTransactionRow(x.Id, x.BankAccountId, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
        var transactionIds = GetInternalTransferTransactionIds(transactionRows);

        await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId && x.Source != "manual")
            .ExecuteDeleteAsync(cancellationToken);

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

    private static List<Guid> GetInternalTransferTransactionIds(IReadOnlyList<InternalTransferTransactionRow> transactions)
    {
        var debits = transactions
            .Where(x => x.AmountMinorUnits < 0)
            .OrderBy(x => x.PostedDate)
            .ToList();
        var credits = transactions
            .Where(x => x.AmountMinorUnits > 0)
            .OrderBy(x => x.PostedDate)
            .ToList();
        var matchedCreditIds = new HashSet<Guid>();
        var internalTransactionIds = new HashSet<Guid>();

        foreach (var debit in debits)
        {
            var credit = credits.FirstOrDefault(x =>
                !matchedCreditIds.Contains(x.Id)
                && x.BankAccountId != debit.BankAccountId
                && x.Currency == debit.Currency
                && x.AmountMinorUnits == Math.Abs(debit.AmountMinorUnits)
                && Math.Abs(x.PostedDate.DayNumber - debit.PostedDate.DayNumber) <= InternalTransferPostingWindowDays);
            if (credit is null)
            {
                continue;
            }

            matchedCreditIds.Add(credit.Id);
            internalTransactionIds.Add(debit.Id);
            internalTransactionIds.Add(credit.Id);
        }

        return internalTransactionIds.ToList();
    }

    private sealed record InternalTransferTransactionRow(Guid Id, Guid BankAccountId, long AmountMinorUnits, string Currency, DateOnly PostedDate);
}
