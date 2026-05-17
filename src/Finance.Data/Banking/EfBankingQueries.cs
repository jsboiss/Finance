namespace Finance.Data.Banking;

using System.Text.Json;
using System.Text.RegularExpressions;
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

    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptions(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var subscriptions = await dbContext.Subscriptions
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return await GetSubscriptionDtos(tenantId, subscriptions, cancellationToken);
    }

    public async Task<SubscriptionDetailDto?> GetSubscription(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var dto = (await GetSubscriptionDtos(tenantId, [subscription], cancellationToken)).Single();
        var payments = await GetSubscriptionPayments(tenantId, subscription.Id, cancellationToken);
        return new SubscriptionDetailDto(dto, payments);
    }

    public async Task<SubscriptionDto> CreateSubscription(CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var subscription = new Subscription
        {
            TenantId = tenantId,
            Name = CleanRequired(request.Name, "Subscription name is required."),
            MerchantName = CleanRequired(request.MerchantName, "Merchant name is required."),
            MerchantKey = GetMerchantKey(request.MerchantName),
            PaymentManager = CleanPaymentManager(request.PaymentManager),
            Cadence = CleanCadence(request.Cadence),
            ExpectedAmountMinorUnits = Math.Abs(request.ExpectedAmountMinorUnits),
            Currency = CleanCurrency(request.Currency),
            StatusOverride = CleanStatusOverride(request.StatusOverride),
            IsCancelled = request.IsCancelled
        };

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RelinkSubscriptionTransactions(tenantId, subscription, cancellationToken);
        return (await GetSubscriptionDtos(tenantId, [subscription], cancellationToken)).Single();
    }

    public async Task<SubscriptionDto?> UpdateSubscription(Guid subscriptionId, UpdateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == subscriptionId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        subscription.Name = CleanRequired(request.Name, "Subscription name is required.");
        subscription.MerchantName = CleanRequired(request.MerchantName, "Merchant name is required.");
        subscription.MerchantKey = GetMerchantKey(request.MerchantName);
        subscription.PaymentManager = CleanPaymentManager(request.PaymentManager);
        subscription.Cadence = CleanCadence(request.Cadence);
        subscription.ExpectedAmountMinorUnits = Math.Abs(request.ExpectedAmountMinorUnits);
        subscription.Currency = CleanCurrency(request.Currency);
        subscription.StatusOverride = CleanStatusOverride(request.StatusOverride);
        subscription.IsCancelled = request.IsCancelled;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RelinkSubscriptionTransactions(tenantId, subscription, cancellationToken);
        return (await GetSubscriptionDtos(tenantId, [subscription], cancellationToken)).Single();
    }

    public async Task<bool> DeleteSubscription(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        await dbContext.SubscriptionTransactions.Where(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId).ExecuteDeleteAsync(cancellationToken);
        return await dbContext.Subscriptions.Where(x => x.TenantId == tenantId && x.Id == subscriptionId).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<SubscriptionSuggestionDto>> GetSubscriptionSuggestions(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var suggestions = await dbContext.SubscriptionSuggestions
            .Where(x => x.TenantId == tenantId && x.Status == "pending")
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.MerchantName)
            .ToListAsync(cancellationToken);

        return suggestions.Select(ToSuggestionDto).ToList();
    }

    public async Task<IReadOnlyList<SubscriptionSuggestionDto>> RefreshSubscriptionSuggestions(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var transactions = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.Status == "posted" && x.AmountMinorUnits < 0)
            .Select(x => new SubscriptionDetectionTransaction(x.Id, x.Description, x.MerchantName, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);

        var existingSubscriptions = await dbContext.Subscriptions
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.MerchantKey)
            .ToListAsync(cancellationToken);
        var subscribedMerchantKeys = existingSubscriptions.ToHashSet();

        var activeSuggestionKeys = new HashSet<string>();
        foreach (var candidate in GetSubscriptionCandidates(transactions))
        {
            if (subscribedMerchantKeys.Contains(candidate.MerchantKey))
            {
                continue;
            }

            activeSuggestionKeys.Add(GetSuggestionKey(candidate.MerchantKey, candidate.Cadence, candidate.ExpectedAmountMinorUnits));
            var suggestion = await dbContext.SubscriptionSuggestions.FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.MerchantKey == candidate.MerchantKey
                    && x.Cadence == candidate.Cadence
                    && x.ExpectedAmountMinorUnits == candidate.ExpectedAmountMinorUnits,
                cancellationToken);

            if (suggestion is { Status: "dismissed" })
            {
                continue;
            }

            if (suggestion is null)
            {
                suggestion = new SubscriptionSuggestion
                {
                    TenantId = tenantId,
                    MerchantName = candidate.MerchantName,
                    MerchantKey = candidate.MerchantKey
                };
                dbContext.SubscriptionSuggestions.Add(suggestion);
            }

            suggestion.PaymentManager = candidate.PaymentManager;
            suggestion.Cadence = candidate.Cadence;
            suggestion.ExpectedAmountMinorUnits = candidate.ExpectedAmountMinorUnits;
            suggestion.Currency = candidate.Currency;
            suggestion.Confidence = candidate.Confidence;
            suggestion.Status = suggestion.Status == "accepted" ? "accepted" : "pending";
            suggestion.SampleTransactionIds = JsonSerializer.Serialize(candidate.SampleTransactionIds);
            suggestion.FirstPaymentDate = candidate.FirstPaymentDate;
            suggestion.LastPaymentDate = candidate.LastPaymentDate;
            suggestion.NextExpectedPaymentDate = candidate.NextExpectedPaymentDate;
            suggestion.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var pendingSuggestions = await dbContext.SubscriptionSuggestions
            .Where(x => x.TenantId == tenantId && x.Status == "pending")
            .ToListAsync(cancellationToken);
        foreach (var suggestion in pendingSuggestions.Where(x => !activeSuggestionKeys.Contains(GetSuggestionKey(x.MerchantKey, x.Cadence, x.ExpectedAmountMinorUnits))))
        {
            dbContext.SubscriptionSuggestions.Remove(suggestion);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetSubscriptionSuggestions(cancellationToken);
    }

    public async Task<SubscriptionDto?> AcceptSubscriptionSuggestion(Guid suggestionId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var suggestion = await dbContext.SubscriptionSuggestions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == suggestionId, cancellationToken);
        if (suggestion is null)
        {
            return null;
        }

        var subscription = await dbContext.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.MerchantKey == suggestion.MerchantKey, cancellationToken);
        if (subscription is null)
        {
            subscription = new Subscription
            {
                TenantId = tenantId,
                Name = suggestion.MerchantName,
                MerchantName = suggestion.MerchantName,
                MerchantKey = suggestion.MerchantKey,
                PaymentManager = suggestion.PaymentManager,
                Cadence = suggestion.Cadence,
                ExpectedAmountMinorUnits = suggestion.ExpectedAmountMinorUnits,
                Currency = suggestion.Currency,
                FirstPaymentDate = suggestion.FirstPaymentDate,
                LastPaymentDate = suggestion.LastPaymentDate,
                NextExpectedPaymentDate = suggestion.NextExpectedPaymentDate
            };
            dbContext.Subscriptions.Add(subscription);
        }

        suggestion.Status = "accepted";
        suggestion.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RelinkSubscriptionTransactions(tenantId, subscription, cancellationToken);
        return (await GetSubscriptionDtos(tenantId, [subscription], cancellationToken)).Single();
    }

    public async Task<bool> DismissSubscriptionSuggestion(Guid suggestionId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var suggestion = await dbContext.SubscriptionSuggestions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == suggestionId, cancellationToken);
        if (suggestion is null)
        {
            return false;
        }

        suggestion.Status = "dismissed";
        suggestion.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionDtos(Guid tenantId, IReadOnlyList<Subscription> subscriptions, CancellationToken cancellationToken)
    {
        var subscriptionIds = subscriptions.Select(x => x.Id).ToList();
        var paymentsBySubscription = await dbContext.SubscriptionTransactions
            .Where(x => x.TenantId == tenantId && subscriptionIds.Contains(x.SubscriptionId))
            .Join(dbContext.BankTransactions.Where(x => x.TenantId == tenantId),
                x => x.BankTransactionId,
                y => y.Id,
                (x, y) => new { x.SubscriptionId, Payment = new SubscriptionPaymentDto(y.Id, y.Description, y.MerchantName, Math.Abs(y.AmountMinorUnits), y.Currency, y.PostedDate) })
            .GroupBy(x => x.SubscriptionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Payment).OrderBy(y => y.PostedDate).ToList(), cancellationToken);

        return subscriptions
            .Select(x =>
            {
                var payments = paymentsBySubscription.GetValueOrDefault(x.Id, []);
                var firstPaymentDate = payments.Count > 0 ? payments.Min(y => y.PostedDate) : x.FirstPaymentDate;
                var lastPaymentDate = payments.Count > 0 ? payments.Max(y => y.PostedDate) : x.LastPaymentDate;
                var nextExpectedPaymentDate = lastPaymentDate is { } lastDate ? AddCadence(lastDate, x.Cadence) : x.NextExpectedPaymentDate;
                var priceChanges = GetPriceChanges(payments);
                var totalPaid = payments.Sum(y => y.AmountMinorUnits);

                x.FirstPaymentDate = firstPaymentDate;
                x.LastPaymentDate = lastPaymentDate;
                x.NextExpectedPaymentDate = nextExpectedPaymentDate;

                return new SubscriptionDto(
                    x.Id,
                    x.Name,
                    x.MerchantName,
                    x.MerchantKey,
                    x.PaymentManager,
                    x.Cadence,
                    x.ExpectedAmountMinorUnits,
                    x.Currency,
                    GetSubscriptionStatus(x, lastPaymentDate, nextExpectedPaymentDate),
                    x.StatusOverride,
                    x.IsCancelled,
                    firstPaymentDate,
                    lastPaymentDate,
                    nextExpectedPaymentDate,
                    totalPaid,
                    GetMonthlyEstimate(x.ExpectedAmountMinorUnits, x.Cadence),
                    GetYearlyEstimate(x.ExpectedAmountMinorUnits, x.Cadence),
                    priceChanges);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SubscriptionPaymentDto>> GetSubscriptionPayments(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken)
    {
        return await dbContext.SubscriptionTransactions
            .Where(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId)
            .Join(dbContext.BankTransactions.Where(x => x.TenantId == tenantId),
                x => x.BankTransactionId,
                y => y.Id,
                (x, y) => y)
            .OrderByDescending(x => x.PostedDate)
            .Select(x => new SubscriptionPaymentDto(x.Id, x.Description, x.MerchantName, Math.Abs(x.AmountMinorUnits), x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
    }

    private async Task RelinkSubscriptionTransactions(Guid tenantId, Subscription subscription, CancellationToken cancellationToken)
    {
        await dbContext.SubscriptionTransactions.Where(x => x.TenantId == tenantId && x.SubscriptionId == subscription.Id).ExecuteDeleteAsync(cancellationToken);
        var transactions = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.Status == "posted" && x.AmountMinorUnits < 0)
            .Select(x => new { x.Id, x.Description, x.MerchantName, x.AmountMinorUnits, x.Currency, x.PostedDate })
            .ToListAsync(cancellationToken);

        var matchingTransactions = transactions
            .Where(x => GetTransactionMerchantKey(x.MerchantName, x.Description) == subscription.MerchantKey
                && x.Currency == subscription.Currency)
            .ToList();

        foreach (var transaction in matchingTransactions)
        {
            dbContext.SubscriptionTransactions.Add(new SubscriptionTransaction
            {
                TenantId = tenantId,
                SubscriptionId = subscription.Id,
                BankTransactionId = transaction.Id,
                MatchConfidence = 90
            });
        }

        if (matchingTransactions.Count > 0)
        {
            subscription.FirstPaymentDate = matchingTransactions.Min(x => x.PostedDate);
            subscription.LastPaymentDate = matchingTransactions.Max(x => x.PostedDate);
            subscription.NextExpectedPaymentDate = AddCadence(subscription.LastPaymentDate.Value, subscription.Cadence);
        }

        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<SubscriptionCandidate> GetSubscriptionCandidates(IReadOnlyList<SubscriptionDetectionTransaction> transactions)
    {
        return transactions
            .GroupBy(x => GetTransactionMerchantKey(x.MerchantName, x.Description))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .SelectMany(x => GetCandidatesForMerchant(x.Key, x.OrderBy(y => y.PostedDate).ToList()))
            .OrderByDescending(x => x.Confidence)
            .ToList();
    }

    private static IReadOnlyList<SubscriptionCandidate> GetCandidatesForMerchant(string merchantKey, IReadOnlyList<SubscriptionDetectionTransaction> transactions)
    {
        var candidates = new List<SubscriptionCandidate>();
        foreach (var amountGroup in GetSimilarAmountGroups(transactions))
        {
            if (amountGroup.Count < 3)
            {
                continue;
            }

            var cadence = InferCadence(amountGroup.Select(x => x.PostedDate).Order().ToList());
            if (cadence is null)
            {
                continue;
            }

            var representative = amountGroup.OrderByDescending(x => x.PostedDate).First();
            var expectedAmount = (long)Math.Round(amountGroup.Average(x => Math.Abs(x.AmountMinorUnits)));
            var firstPaymentDate = amountGroup.Min(x => x.PostedDate);
            var lastPaymentDate = amountGroup.Max(x => x.PostedDate);
            var consistency = amountGroup.Count(x => AmountsAreSimilar(Math.Abs(x.AmountMinorUnits), expectedAmount));
            var confidence = Math.Clamp(60 + amountGroup.Count * 8 + consistency * 4, 0, 98);
            candidates.Add(new SubscriptionCandidate(
                GetDisplayMerchantName(representative.MerchantName, representative.Description),
                merchantKey,
                GetPaymentManager(merchantKey),
                cadence,
                expectedAmount,
                representative.Currency,
                confidence,
                amountGroup.TakeLast(5).Select(x => x.Id).ToList(),
                firstPaymentDate,
                lastPaymentDate,
                AddCadence(lastPaymentDate, cadence)));
        }

        return candidates
            .GroupBy(x => new { x.MerchantKey, x.Cadence, x.Currency })
            .Select(x => x.OrderByDescending(y => y.LastPaymentDate).ThenByDescending(y => y.Confidence).First())
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<SubscriptionDetectionTransaction>> GetSimilarAmountGroups(IReadOnlyList<SubscriptionDetectionTransaction> transactions)
    {
        var groups = new List<List<SubscriptionDetectionTransaction>>();
        foreach (var transaction in transactions)
        {
            var amount = Math.Abs(transaction.AmountMinorUnits);
            var group = groups.FirstOrDefault(x => AmountsAreSimilar(Math.Abs(x[0].AmountMinorUnits), amount));
            if (group is null)
            {
                groups.Add([transaction]);
            }
            else
            {
                group.Add(transaction);
            }
        }

        return groups;
    }

    private static string? InferCadence(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count < 3)
        {
            return null;
        }

        var gaps = dates.Zip(dates.Skip(1), (x, y) => y.DayNumber - x.DayNumber).ToList();
        var averageGap = gaps.Average();
        return averageGap switch
        {
            >= 6 and <= 8 => "weekly",
            >= 12 and <= 16 => "fortnightly",
            >= 26 and <= 35 => "monthly",
            >= 350 and <= 380 => "yearly",
            _ => null
        };
    }

    private static IReadOnlyList<SubscriptionPriceChangeDto> GetPriceChanges(IReadOnlyList<SubscriptionPaymentDto> payments)
    {
        var orderedPayments = payments.OrderBy(x => x.PostedDate).ToList();
        var changes = new List<SubscriptionPriceChangeDto>();
        if (orderedPayments.Count < 2)
        {
            return changes;
        }

        var previousAmount = orderedPayments[0].AmountMinorUnits;
        for (var index = 1; index < orderedPayments.Count; index++)
        {
            var payment = orderedPayments[index];
            if (payment.AmountMinorUnits <= previousAmount || AmountsAreSimilar(payment.AmountMinorUnits, previousAmount))
            {
                previousAmount = payment.AmountMinorUnits;
                continue;
            }

            var repeated = orderedPayments.Skip(index + 1).Any(x => AmountsAreSimilar(x.AmountMinorUnits, payment.AmountMinorUnits));
            changes.Add(new SubscriptionPriceChangeDto(payment.PostedDate, previousAmount, payment.AmountMinorUnits, repeated ? "confirmed" : "possible"));
            previousAmount = payment.AmountMinorUnits;
        }

        return changes;
    }

    private static bool AmountsAreSimilar(long amount, long otherAmount)
    {
        var tolerance = Math.Max(200, (long)Math.Round(Math.Max(amount, otherAmount) * 0.05));
        return Math.Abs(amount - otherAmount) <= tolerance;
    }

    private static string GetSubscriptionStatus(Subscription subscription, DateOnly? lastPaymentDate, DateOnly? nextExpectedPaymentDate)
    {
        if (!string.IsNullOrWhiteSpace(subscription.StatusOverride))
        {
            return subscription.StatusOverride;
        }

        if (lastPaymentDate is null || nextExpectedPaymentDate is null)
        {
            return "needsReview";
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var graceDays = subscription.Cadence == "yearly" ? 21 : 7;
        var inactiveDays = subscription.Cadence == "yearly" ? 90 : 45;
        if (today <= nextExpectedPaymentDate.Value.AddDays(graceDays))
        {
            return "active";
        }

        return today > nextExpectedPaymentDate.Value.AddDays(inactiveDays) ? "inactive" : "needsReview";
    }

    private static DateOnly AddCadence(DateOnly date, string cadence)
    {
        return cadence switch
        {
            "weekly" => date.AddDays(7),
            "fortnightly" => date.AddDays(14),
            "yearly" => date.AddYears(1),
            _ => date.AddMonths(1)
        };
    }

    private static long GetMonthlyEstimate(long amount, string cadence)
    {
        return cadence switch
        {
            "weekly" => amount * 52 / 12,
            "fortnightly" => amount * 26 / 12,
            "yearly" => amount / 12,
            _ => amount
        };
    }

    private static long GetYearlyEstimate(long amount, string cadence)
    {
        return cadence switch
        {
            "weekly" => amount * 52,
            "fortnightly" => amount * 26,
            "yearly" => amount,
            _ => amount * 12
        };
    }

    private static SubscriptionSuggestionDto ToSuggestionDto(SubscriptionSuggestion suggestion)
    {
        var sampleTransactionIds = JsonSerializer.Deserialize<IReadOnlyList<Guid>>(suggestion.SampleTransactionIds) ?? [];
        return new SubscriptionSuggestionDto(
            suggestion.Id,
            suggestion.MerchantName,
            suggestion.MerchantKey,
            suggestion.PaymentManager,
            suggestion.Cadence,
            suggestion.ExpectedAmountMinorUnits,
            suggestion.Currency,
            suggestion.Confidence,
            suggestion.Status,
            suggestion.FirstPaymentDate,
            suggestion.LastPaymentDate,
            suggestion.NextExpectedPaymentDate,
            sampleTransactionIds);
    }

    private static string CleanRequired(string value, string message)
    {
        var trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            throw new InvalidOperationException(message);
        }

        return trimmedValue;
    }

    private static string CleanCadence(string cadence)
    {
        return cadence.Trim().ToLowerInvariant() switch
        {
            "weekly" => "weekly",
            "fortnightly" => "fortnightly",
            "yearly" => "yearly",
            _ => "monthly"
        };
    }

    private static string CleanPaymentManager(string? paymentManager)
    {
        return string.IsNullOrWhiteSpace(paymentManager) ? "direct" : paymentManager.Trim().ToLowerInvariant();
    }

    private static string CleanCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency) ? "AUD" : currency.Trim().ToUpperInvariant();
    }

    private static string? CleanStatusOverride(string? statusOverride)
    {
        if (string.IsNullOrWhiteSpace(statusOverride))
        {
            return null;
        }

        return statusOverride.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "needsreview" => "needsReview",
            "needsReview" => "needsReview",
            "inactive" => "inactive",
            _ => null
        };
    }

    private static string GetTransactionMerchantKey(string? merchantName, string description)
    {
        return GetMerchantKey(GetDisplayMerchantName(merchantName, description));
    }

    private static string GetDisplayMerchantName(string? merchantName, string description)
    {
        return string.IsNullOrWhiteSpace(merchantName) ? description.Trim() : merchantName.Trim();
    }

    private static string GetPaymentManager(string merchantKey)
    {
        if (merchantKey.Contains("apple"))
        {
            return "apple";
        }

        if (merchantKey.Contains("paypal") || merchantKey.Contains("pay pal"))
        {
            return "paypal";
        }

        return "direct";
    }

    private static string GetSuggestionKey(string merchantKey, string cadence, long expectedAmountMinorUnits)
    {
        return $"{merchantKey}|{cadence}|{expectedAmountMinorUnits}";
    }

    private static string NormalizeKey(string value)
    {
        var normalizedValue = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        var ignoredTokens = new HashSet<string> { "au", "aus", "vi", "pty", "ltd", "limited", "australia", "melbourne", "sydney", "brisbane", "card", "com" };
        return string.Join(" ", normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => !ignoredTokens.Contains(x)));
    }

    private static string GetAccountDisplayName(string name, string accountNumber)
    {
        return string.IsNullOrWhiteSpace(accountNumber) ? name : $"{name} - {accountNumber}";
    }

    private async Task ApplyMerchantTag(Guid tenantId, string merchantKey, Guid tagId, CancellationToken cancellationToken)
    {
        var merchantRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.MerchantName != null)
            .Select(x => new { x.Id, x.MerchantName })
            .ToListAsync(cancellationToken);
        var transactionIds = merchantRows
            .Where(x => x.MerchantName is not null && GetMerchantKey(x.MerchantName) == merchantKey)
            .Select(x => x.Id)
            .ToList();

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
        return NormalizeKey(merchantName);
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

    private sealed record SubscriptionDetectionTransaction(Guid Id, string Description, string? MerchantName, long AmountMinorUnits, string Currency, DateOnly PostedDate);

    private sealed record SubscriptionCandidate(
        string MerchantName,
        string MerchantKey,
        string PaymentManager,
        string Cadence,
        long ExpectedAmountMinorUnits,
        string Currency,
        int Confidence,
        IReadOnlyList<Guid> SampleTransactionIds,
        DateOnly FirstPaymentDate,
        DateOnly LastPaymentDate,
        DateOnly NextExpectedPaymentDate);
}
