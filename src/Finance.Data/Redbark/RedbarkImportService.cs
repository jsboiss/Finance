namespace Finance.Data.Redbark;

using System.Globalization;
using System.Text.Json;
using Finance.Core.Abstractions;
using Finance.Core.Redbark;
using Finance.Data.Banking;
using Finance.Data.Data;
using Microsoft.EntityFrameworkCore;

public sealed class RedbarkImportService(FinanceDbContext dbContext, IRedbarkClient redbarkClient) : IRedbarkImportService
{
    public Task DiscoverAccounts(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportAccounts(tenantId, cancellationToken);
    }

    public Task Backfill(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), DateOnly.FromDateTime(DateTime.UtcNow), "backfill", cancellationToken);
    }

    public Task BackfillAccount(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        return ImportAccountRange(tenantId, accountId, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
    }

    public Task ReconcileRecent(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow), "recent-reconciliation", cancellationToken);
    }

    public Task ReconcileFull(Guid tenantId, CancellationToken cancellationToken)
    {
        return ImportRange(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-24)), DateOnly.FromDateTime(DateTime.UtcNow), "full-reconciliation", cancellationToken);
    }

    public async Task ProcessWebhook(Guid tenantId, string eventId, string eventType, string rawJson, CancellationToken cancellationToken)
    {
        var webhookEvent = await dbContext.WebhookEvents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalEventId == eventId, cancellationToken);
        if (webhookEvent?.ProcessedAt is not null)
        {
            return;
        }

        if (webhookEvent is null)
        {
            webhookEvent = new WebhookEvent { TenantId = tenantId, ExternalEventId = eventId };
            dbContext.WebhookEvents.Add(webhookEvent);
        }

        webhookEvent.EventType = eventType;
        webhookEvent.RawJson = rawJson;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (eventType == "transactions.synced")
        {
            await ProcessTransactionWebhook(tenantId, rawJson, cancellationToken);
        }

        webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessTransactionWebhook(Guid tenantId, string rawJson, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(rawJson);
        var data = document.RootElement.GetProperty("data");
        var transactions = data.GetProperty("new").EnumerateArray()
            .Concat(data.GetProperty("updated").EnumerateArray())
            .Where(x => GetNullableString(x, "status")?.Equals("posted", StringComparison.OrdinalIgnoreCase) ?? true)
            .Select(ToTransactionDto)
            .ToList();

        if (transactions.Count == 0)
        {
            return;
        }

        var externalAccountIds = transactions.Select(x => x.AccountId).Distinct().ToList();
        var accountsByExternalId = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && externalAccountIds.Contains(x.ExternalAccountId))
            .ToDictionaryAsync(x => x.ExternalAccountId, cancellationToken);

        var missingAccountIds = externalAccountIds.Where(x => !accountsByExternalId.ContainsKey(x)).ToList();
        if (missingAccountIds.Count > 0)
        {
            throw new InvalidOperationException($"Webhook referenced unknown account IDs: {string.Join(", ", missingAccountIds)}. Run account discovery first.");
        }

        var importedCount = 0;
        foreach (var transaction in transactions)
        {
            importedCount += await UpsertTransaction(tenantId, accountsByExternalId[transaction.AccountId].Id, transaction, cancellationToken) ? 1 : 0;
        }

        dbContext.ImportRuns.Add(new ImportRun
        {
            TenantId = tenantId,
            Source = "webhook: transactions.synced",
            Status = "completed",
            ImportedCount = importedCount,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ImportRange(Guid tenantId, DateOnly from, DateOnly to, string source, CancellationToken cancellationToken)
    {
        var run = new ImportRun { TenantId = tenantId, Source = source };
        dbContext.ImportRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DefaultBankingData.EnsureDefaultTags(tenantId, dbContext, cancellationToken);

        try
        {
            var importedCount = 0;
            var connections = await redbarkClient.GetConnections(tenantId, cancellationToken);
            foreach (var connection in connections)
            {
                var bankConnection = await UpsertConnection(tenantId, connection, cancellationToken);
                var accounts = await redbarkClient.GetAccounts(tenantId, connection.Id, cancellationToken);
                var bankAccountsByExternalId = new Dictionary<string, BankAccount>();
                foreach (var account in accounts)
                {
                    var bankAccount = await UpsertAccount(tenantId, bankConnection.Id, account, cancellationToken);
                    bankAccountsByExternalId[account.Id] = bankAccount;

                    string? cursor = null;
                    do
                    {
                        var page = await redbarkClient.GetTransactions(tenantId, connection.Id, account.Id, from, to, cursor, cancellationToken);
                        foreach (var transaction in page.Transactions.Where(x => x.Status.Equals("posted", StringComparison.OrdinalIgnoreCase)))
                        {
                            importedCount += await UpsertTransaction(tenantId, bankAccount.Id, transaction, cancellationToken) ? 1 : 0;
                        }

                        cursor = page.NextCursor;
                    }
                    while (!string.IsNullOrWhiteSpace(cursor));
                }

                var balances = await redbarkClient.GetBalances(tenantId, accounts.Select(x => x.Id).ToList(), cancellationToken);
                foreach (var balance in balances)
                {
                    if (bankAccountsByExternalId.TryGetValue(balance.AccountId, out var bankAccount))
                    {
                        await UpsertBalance(tenantId, bankAccount.Id, balance, cancellationToken);
                    }
                }
            }

            run.ImportedCount = importedCount;
            run.Status = "completed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ImportAccounts(Guid tenantId, CancellationToken cancellationToken)
    {
        var run = new ImportRun { TenantId = tenantId, Source = "account-discovery" };
        dbContext.ImportRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DefaultBankingData.EnsureDefaultTags(tenantId, dbContext, cancellationToken);

        try
        {
            var importedCount = 0;
            var connections = await redbarkClient.GetConnections(tenantId, cancellationToken);
            foreach (var connection in connections)
            {
                var bankConnection = await UpsertConnection(tenantId, connection, cancellationToken);
                var accounts = await redbarkClient.GetAccounts(tenantId, connection.Id, cancellationToken);
                var bankAccountsByExternalId = new Dictionary<string, BankAccount>();
                foreach (var account in accounts)
                {
                    var bankAccount = await UpsertAccount(tenantId, bankConnection.Id, account, cancellationToken);
                    bankAccountsByExternalId[account.Id] = bankAccount;
                    importedCount++;
                }

                var balances = await redbarkClient.GetBalances(tenantId, accounts.Select(x => x.Id).ToList(), cancellationToken);
                foreach (var balance in balances)
                {
                    if (bankAccountsByExternalId.TryGetValue(balance.AccountId, out var bankAccount))
                    {
                        await UpsertBalance(tenantId, bankAccount.Id, balance, cancellationToken);
                    }
                }
            }

            run.ImportedCount = importedCount;
            run.Status = "completed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ImportAccountRange(Guid tenantId, Guid accountId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var run = new ImportRun { TenantId = tenantId, Source = "account-backfill" };
        dbContext.ImportRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DefaultBankingData.EnsureDefaultTags(tenantId, dbContext, cancellationToken);

        try
        {
            var account = await dbContext.BankAccounts
                .Join(dbContext.BankConnections.Where(x => x.TenantId == tenantId),
                    x => x.BankConnectionId,
                    y => y.Id,
                    (x, y) => new { Account = x, Connection = y })
                .FirstOrDefaultAsync(x => x.Account.TenantId == tenantId && x.Account.Id == accountId, cancellationToken);

            if (account is null)
            {
                throw new InvalidOperationException("Account was not found for this tenant. Run account discovery first.");
            }

            run.Source = $"account-backfill: {GetAccountDisplayName(account.Account)}";
            await dbContext.SaveChangesAsync(cancellationToken);

            var importedCount = 0;
            string? cursor = null;
            do
            {
                var page = await redbarkClient.GetTransactions(tenantId, account.Connection.ExternalConnectionId, account.Account.ExternalAccountId, from, to, cursor, cancellationToken);
                foreach (var transaction in page.Transactions.Where(x => x.Status.Equals("posted", StringComparison.OrdinalIgnoreCase)))
                {
                    importedCount += await UpsertTransaction(tenantId, account.Account.Id, transaction, cancellationToken) ? 1 : 0;
                }

                cursor = page.NextCursor;
            }
            while (!string.IsNullOrWhiteSpace(cursor));

            var balances = await redbarkClient.GetBalances(tenantId, [account.Account.ExternalAccountId], cancellationToken);
            foreach (var balance in balances)
            {
                await UpsertBalance(tenantId, account.Account.Id, balance, cancellationToken);
            }

            run.ImportedCount = importedCount;
            run.Status = "completed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string GetAccountDisplayName(BankAccount account)
    {
        var name = string.IsNullOrWhiteSpace(account.CustomName) ? account.Name : account.CustomName;
        return string.IsNullOrWhiteSpace(account.AccountNumber) ? name : $"{name} - {account.AccountNumber}";
    }

    private static RedbarkTransactionDto ToTransactionDto(JsonElement transaction)
    {
        return new RedbarkTransactionDto(
            transaction.GetProperty("id").GetString()!,
            transaction.GetProperty("account_id").GetString()!,
            GetNullableString(transaction, "account_name") ?? GetNullableString(transaction, "account") ?? "",
            transaction.GetProperty("description").GetString() ?? "",
            GetNullableString(transaction, "merchant_name"),
            GetNullableString(transaction, "merchant_category_code"),
            GetNullableString(transaction, "category") ?? "Uncategorized",
            transaction.GetProperty("amount").GetInt64(),
            (GetNullableString(transaction, "currency") ?? "AUD").ToUpperInvariant(),
            DateOnly.Parse(transaction.GetProperty("local_date").GetString()!, CultureInfo.InvariantCulture),
            ParseNullableDateTimeOffset(GetNullableString(transaction, "post_date") ?? GetNullableString(transaction, "transaction_date")),
            GetNullableString(transaction, "direction") ?? "",
            GetNullableString(transaction, "status") ?? "posted",
            JsonDocument.Parse(transaction.GetRawText()));
    }

    private async Task<BankConnection> UpsertConnection(Guid tenantId, RedbarkConnectionDto connection, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalConnectionId == connection.Id, cancellationToken);
        if (entity is null)
        {
            entity = new BankConnection { TenantId = tenantId, ExternalConnectionId = connection.Id };
            dbContext.BankConnections.Add(entity);
        }

        entity.InstitutionName = connection.InstitutionName;
        entity.RawJson = connection.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<BankAccount> UpsertAccount(Guid tenantId, Guid connectionId, RedbarkAccountDto account, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankAccounts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalAccountId == account.Id, cancellationToken);
        if (entity is null)
        {
            entity = new BankAccount { TenantId = tenantId, ExternalAccountId = account.Id };
            dbContext.BankAccounts.Add(entity);
        }

        entity.BankConnectionId = connectionId;
        entity.Name = account.Name;
        entity.AccountNumber = account.AccountNumber;
        entity.Currency = account.Currency;
        entity.RawJson = account.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task UpsertBalance(Guid tenantId, Guid accountId, RedbarkBalanceDto balance, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Balances.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BankAccountId == accountId && x.AsOf == balance.AsOf, cancellationToken);
        if (entity is null)
        {
            entity = new Balance { TenantId = tenantId, BankAccountId = accountId, AsOf = balance.AsOf };
            dbContext.Balances.Add(entity);
        }

        entity.CurrentMinorUnits = balance.CurrentMinorUnits;
        entity.Currency = balance.Currency;
        entity.RawJson = balance.Raw;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> UpsertTransaction(Guid tenantId, Guid accountId, RedbarkTransactionDto transaction, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BankTransactions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalTransactionId == transaction.Id, cancellationToken);
        var isNew = entity is null;
        if (entity is null)
        {
            entity = new BankTransaction { TenantId = tenantId, ExternalTransactionId = transaction.Id };
            dbContext.BankTransactions.Add(entity);
        }

        entity.BankAccountId = accountId;
        entity.ExternalAccountName = transaction.AccountName;
        entity.Description = transaction.Description;
        entity.MerchantName = transaction.MerchantName;
        entity.MerchantCategoryCode = transaction.MerchantCategoryCode;
        entity.Category = string.IsNullOrWhiteSpace(transaction.Category) ? "Uncategorized" : transaction.Category;
        entity.AmountMinorUnits = transaction.AmountMinorUnits;
        entity.Currency = transaction.Currency;
        entity.PostedDate = transaction.PostedDate;
        entity.PostedAt = transaction.PostedAt;
        entity.Direction = transaction.Direction;
        entity.Status = transaction.Status;
        entity.RawJson = transaction.Raw;
        await ApplyMerchantTags(tenantId, entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return isNew;
    }

    private async Task ApplyMerchantTags(Guid tenantId, BankTransaction transaction, CancellationToken cancellationToken)
    {
        var merchantName = string.IsNullOrWhiteSpace(transaction.MerchantName) ? transaction.Description : transaction.MerchantName;
        if (string.IsNullOrWhiteSpace(merchantName))
        {
            return;
        }

        var merchantKey = DefaultBankingData.GetMerchantKey(merchantName);
        var tagIds = await dbContext.MerchantTags
            .Where(x => x.TenantId == tenantId && x.MerchantKey == merchantKey)
            .Select(x => x.TransactionTagId)
            .ToListAsync(cancellationToken);

        if (tagIds.Count == 0)
        {
            return;
        }

        var existingTagIds = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && x.BankTransactionId == transaction.Id && tagIds.Contains(x.TransactionTagId))
            .Select(x => x.TransactionTagId)
            .ToListAsync(cancellationToken);

        foreach (var tagId in tagIds.Except(existingTagIds))
        {
            dbContext.BankTransactionTags.Add(new BankTransactionTag { TenantId = tenantId, BankTransactionId = transaction.Id, TransactionTagId = tagId, Source = "merchant" });
        }
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind is not JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}
