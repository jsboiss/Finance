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
            .OrderBy(x => x.CustomName == "" ? x.Name : x.CustomName)
            .Select(x => new AccountRow(x.Id, x.BankConnectionId, x.Name, x.CustomName, x.AccountNumber, x.Currency))
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
                x.CustomName,
                x.AccountNumber,
                GetAccountDisplayName(x.Name, x.CustomName, x.AccountNumber),
                institutions.GetValueOrDefault(x.BankConnectionId, ""),
                x.Currency,
                latestBalances.GetValueOrDefault(x.Id)))
            .ToList();
    }

    public async Task<AccountDto?> UpdateAccount(Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var account = await dbContext.BankAccounts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == accountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        account.CustomName = request.CustomName?.Trim() ?? "";
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAccountDtos(tenantId, accountId, cancellationToken)).FirstOrDefault();
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

    public async Task<OverviewDto> GetOverview(Guid? accountId, bool? includeInternalTransfers, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonthKey = $"{today.Year:D4}-{today.Month:D2}";
        var firstVisibleMonth = today.AddMonths(-5);
        var from = new DateOnly(firstVisibleMonth.Year, firstVisibleMonth.Month, 1);
        var shouldIncludeInternalTransfers = ShouldIncludeInternalTransfers(accountId, includeInternalTransfers);

        var accountIds = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && (accountId == null || x.Id == accountId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var balance = await dbContext.Balances
            .Where(x => x.TenantId == tenantId && accountIds.Contains(x.BankAccountId))
            .GroupBy(x => x.BankAccountId)
            .Select(x => x.OrderByDescending(y => y.AsOf).Select(y => y.CurrentMinorUnits).FirstOrDefault())
            .ToListAsync(cancellationToken);

        var transactionRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId
                && accountIds.Contains(x.BankAccountId)
                && x.Status == "posted"
                && x.PostedDate >= from)
            .Select(x => new OverviewTransactionRow(x.Id, x.AmountMinorUnits, x.PostedDate))
            .ToListAsync(cancellationToken);

        var transactionIds = transactionRows.Select(x => x.Id).ToList();
        var internalTagName = DefaultBankingData.InternalTransferTagName.ToLowerInvariant();
        var tagsByTransaction = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BankTransactionId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BankTransactionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);

        var monthKeys = Enumerable.Range(0, 6)
            .Select(x => from.AddMonths(x))
            .Select(x => $"{x.Year:D4}-{x.Month:D2}")
            .ToList();
        var monthRows = monthKeys.Select(x => new OverviewMonthAccumulator(x, FormatMonthLabel(x))).ToList();
        var monthMap = monthRows.ToDictionary(x => x.Key);
        var tagMap = new Dictionary<Guid, OverviewTagAccumulator>();
        var expensesCount = 0;
        var taggedCount = 0;
        long currentMonthIncome = 0;

        foreach (var transaction in transactionRows)
        {
            var tags = tagsByTransaction.GetValueOrDefault(transaction.Id, []);
            if (!shouldIncludeInternalTransfers && tags.Any(x => x.Name.ToLowerInvariant() == internalTagName))
            {
                continue;
            }

            var monthKey = $"{transaction.PostedDate.Year:D4}-{transaction.PostedDate.Month:D2}";
            if (monthKey == currentMonthKey && transaction.AmountMinorUnits > 0)
            {
                currentMonthIncome += transaction.AmountMinorUnits;
            }

            if (transaction.AmountMinorUnits >= 0 || !monthMap.TryGetValue(monthKey, out var month))
            {
                continue;
            }

            expensesCount += 1;
            var spend = Math.Abs(transaction.AmountMinorUnits);
            month.TotalMinorUnits += spend;
            var spendingTags = tags.Count > 0 ? tags : [new TransactionTagDto(Guid.Empty, "Untagged", "#94a3b8")];

            if (tags.Count > 0)
            {
                taggedCount += 1;
            }

            var monthIndex = monthKeys.IndexOf(monthKey);
            for (var index = 0; index < spendingTags.Count; index++)
            {
                var tag = spendingTags[index];
                var tagSpend = spend / spendingTags.Count;
                if (index < spend % spendingTags.Count)
                {
                    tagSpend += 1;
                }

                if (!tagMap.TryGetValue(tag.Id, out var tagAccumulator))
                {
                    tagAccumulator = new OverviewTagAccumulator(tag.Id, tag.Name, tag.Color, monthKeys.Select(x => 0L).ToList());
                    tagMap[tag.Id] = tagAccumulator;
                }

                tagAccumulator.TotalMinorUnits += tagSpend;
                tagAccumulator.Months[monthIndex] += tagSpend;
                month.Tags[tag.Id] = month.Tags.GetValueOrDefault(tag.Id) + tagSpend;
            }
        }

        var topTags = tagMap.Values
            .OrderByDescending(x => x.TotalMinorUnits)
            .Take(8)
            .Select(x => new OverviewTagSpendDto(
                x.Id,
                x.Name,
                x.Color,
                x.TotalMinorUnits,
                x.Months[^1],
                x.Months.Count > 1 ? x.Months[^2] : 0,
                x.Months))
            .ToList();

        var currentMonthSpend = monthRows.LastOrDefault()?.TotalMinorUnits ?? 0;
        var averageDailySpend = currentMonthSpend / GetElapsedDaysInMonth(currentMonthKey);
        await UpsertAverageDailySpendSnapshot(tenantId, accountId, shouldIncludeInternalTransfers, today, averageDailySpend, cancellationToken);
        var monthDtos = monthRows
            .Select(x => new OverviewMonthSpendDto(
                x.Key,
                x.Label,
                x.TotalMinorUnits,
                x.Tags.Select(y => new OverviewMonthTagSpendDto(y.Key, y.Value)).ToList()))
            .ToList();

        return new OverviewDto(
            balance.Count > 0 ? balance.Sum(x => x ?? 0) : null,
            currentMonthSpend,
            averageDailySpend,
            expensesCount > 0 ? (int)Math.Round(taggedCount / (double)expensesCount * 100) : 0,
            currentMonthKey,
            FormatFullMonthLabel(currentMonthKey),
            GetTimeframeLabel(monthRows),
            currentMonthIncome,
            monthDtos,
            topTags,
            GetDailyCashFlowDtos(transactionRows, tagsByTransaction, shouldIncludeInternalTransfers, DefaultBankingData.InternalTransferTagName, "1m"));
    }

    public async Task<IReadOnlyList<OverviewMetricSnapshotDto>> GetAverageDailySpendHistory(Guid? accountId, bool? includeInternalTransfers, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddMonths(-6).AddDays(1);
        var shouldIncludeInternalTransfers = ShouldIncludeInternalTransfers(accountId, includeInternalTransfers);
        var scopeKey = GetOverviewMetricScopeKey(accountId, shouldIncludeInternalTransfers);
        var accountIds = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && (accountId == null || x.Id == accountId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (accountId is not null && accountIds.Count == 0)
        {
            return [];
        }

        var existingSnapshots = await dbContext.OverviewMetricSnapshots
            .Where(x => x.TenantId == tenantId && x.ScopeKey == scopeKey && x.SnapshotDate >= from && x.SnapshotDate <= today)
            .ToListAsync(cancellationToken);
        var snapshotMap = existingSnapshots.ToDictionary(x => x.SnapshotDate);
        var values = await CalculateAverageDailySpendHistory(tenantId, accountIds, shouldIncludeInternalTransfers, from, today, cancellationToken);
        foreach (var snapshotDate in Enumerable.Range(0, today.DayNumber - from.DayNumber + 1).Select(x => from.AddDays(x)))
        {
            var value = values.GetValueOrDefault(snapshotDate);
            if (snapshotMap.TryGetValue(snapshotDate, out var snapshot))
            {
                snapshot.AverageDailySpendMinorUnits = value;
                snapshot.UpdatedAt = DateTimeOffset.UtcNow;
                continue;
            }

            dbContext.OverviewMetricSnapshots.Add(new OverviewMetricSnapshot
            {
                TenantId = tenantId,
                BankAccountId = accountId,
                ScopeKey = scopeKey,
                SnapshotDate = snapshotDate,
                AverageDailySpendMinorUnits = value
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.OverviewMetricSnapshots
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ScopeKey == scopeKey && x.SnapshotDate >= from && x.SnapshotDate <= today)
            .OrderBy(x => x.SnapshotDate)
            .Select(x => new OverviewMetricSnapshotDto(x.SnapshotDate.ToString("yyyy-MM-dd"), x.AverageDailySpendMinorUnits))
            .ToListAsync(cancellationToken);
    }

    public async Task<SavingsTrajectoryDto?> GetSavingsTrajectory(Guid accountId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddMonths(-6).AddDays(1);
        var projectionTo = today.AddMonths(6);
        var account = await dbContext.BankAccounts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == accountId)
            .Select(x => new { x.Id, x.Currency })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        var rows = await dbContext.BankTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BankAccountId == accountId
                && x.Status == "posted"
                && x.PostedDate >= from
                && x.PostedDate <= today
                && x.AmountMinorUnits != 0)
            .Select(x => new SavingsTrajectoryTransactionRow(x.Id, x.AmountMinorUnits, x.PostedDate, x.Description))
            .ToListAsync(cancellationToken);
        var temporaryDepositIds = GetTemporarySavingsDepositIds(rows);

        var rowsByDate = rows
            .GroupBy(x => x.PostedDate)
            .ToDictionary(
                x => x.Key,
                x => new SavingsTrajectoryDay(
                    x.Where(y => y.AmountMinorUnits > 0 && !IsInterestTransaction(y.Description)).Sum(y => y.AmountMinorUnits),
                    x.Where(y => y.AmountMinorUnits > 0 && IsInterestTransaction(y.Description)).Sum(y => y.AmountMinorUnits),
                    x.Where(y => y.AmountMinorUnits < 0).Sum(y => Math.Abs(y.AmountMinorUnits))));
        var actual = new List<SavingsTrajectoryPointDto>();
        var startingBalance = await dbContext.Balances
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BankAccountId == accountId)
            .OrderByDescending(x => x.AsOf)
            .Select(x => x.CurrentMinorUnits)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
        var historicalContributionBalance = rows.Sum(x => x.AmountMinorUnits);
        var runningBalance = startingBalance - historicalContributionBalance;
        var dailyBalances = new List<long>();

        foreach (var date in Enumerable.Range(0, today.DayNumber - from.DayNumber + 1).Select(x => from.AddDays(x)))
        {
            var day = rowsByDate.GetValueOrDefault(date, new SavingsTrajectoryDay(0, 0, 0));
            runningBalance += day.DepositMinorUnits + day.InterestMinorUnits - day.WithdrawalMinorUnits;
            actual.Add(new SavingsTrajectoryPointDto(date.ToString("yyyy-MM-dd"), runningBalance, day.DepositMinorUnits, day.InterestMinorUnits, day.WithdrawalMinorUnits));
            dailyBalances.Add(Math.Max(0, runningBalance));
        }

        var totalDeposits = rows
            .Where(x => x.AmountMinorUnits > 0 && !IsInterestTransaction(x.Description) && !temporaryDepositIds.Contains(x.Id))
            .Sum(x => x.AmountMinorUnits);
        var totalInterest = actual.Sum(x => x.InterestMinorUnits);
        var elapsedDays = Math.Max(1, today.DayNumber - from.DayNumber + 1);
        var projectedDailyDeposits = totalDeposits / (decimal)elapsedDays;
        var balanceDays = Math.Max(1, dailyBalances.Sum(x => (decimal)x));
        var projectedDailyInterestRate = totalInterest > 0 ? totalInterest / balanceDays : 0;
        var projection = new List<SavingsTrajectoryPointDto>();
        var projectedBalance = runningBalance;

        foreach (var date in Enumerable.Range(1, projectionTo.DayNumber - today.DayNumber).Select(x => today.AddDays(x)))
        {
            var projectedDeposit = (long)Math.Round(projectedDailyDeposits, MidpointRounding.AwayFromZero);
            var projectedInterest = (long)Math.Round(projectedBalance * projectedDailyInterestRate, MidpointRounding.AwayFromZero);
            projectedBalance += projectedDeposit + projectedInterest;
            projection.Add(new SavingsTrajectoryPointDto(date.ToString("yyyy-MM-dd"), projectedBalance, projectedDeposit, projectedInterest, 0));
        }

        return new SavingsTrajectoryDto(
            account.Id,
            account.Currency,
            totalDeposits,
            totalInterest,
            (long)Math.Round(projectedDailyDeposits * 30, MidpointRounding.AwayFromZero),
            projection.Take(30).Sum(x => x.InterestMinorUnits),
            actual,
            projection);
    }

    public async Task<IReadOnlyList<OverviewDailyCashFlowDto>> GetDailyCashFlow(Guid? accountId, bool? includeInternalTransfers, string? range, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = GetDailyCashFlowStart(today, range);
        var shouldIncludeInternalTransfers = ShouldIncludeInternalTransfers(accountId, includeInternalTransfers);

        var accountIds = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && (accountId == null || x.Id == accountId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var transactionRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId
                && accountIds.Contains(x.BankAccountId)
                && x.Status == "posted"
                && x.PostedDate >= from
                && x.PostedDate <= today)
            .Select(x => new OverviewTransactionRow(x.Id, x.AmountMinorUnits, x.PostedDate))
            .ToListAsync(cancellationToken);

        var transactionIds = transactionRows.Select(x => x.Id).ToList();
        var tagsByTransaction = await dbContext.BankTransactionTags
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BankTransactionId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BankTransactionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);

        return GetDailyCashFlowDtos(transactionRows, tagsByTransaction, shouldIncludeInternalTransfers, DefaultBankingData.InternalTransferTagName, range);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetTransactions(TransactionQuery query, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var transactions = dbContext.BankTransactions.AsNoTracking().Where(x => x.TenantId == tenantId);

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
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accountIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.CustomName, x.AccountNumber })
            .ToDictionaryAsync(x => x.Id, x => new AccountDisplay(x.Name, x.CustomName, x.AccountNumber), cancellationToken);

        var transactionIds = transactionRows.Select(x => x.Id).ToList();
        var tagsByTransaction = await dbContext.BankTransactionTags
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.AsNoTracking().Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BankTransactionId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BankTransactionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);

        return transactionRows
            .Select(x =>
            {
                var account = accountDisplays.GetValueOrDefault(x.AccountId, new AccountDisplay(x.ExternalAccountName, "", ""));
                return new TransactionDto(
                    x.Id,
                    x.AccountId,
                    x.ExternalTransactionId,
                    GetAccountReferenceName(account.Name, account.CustomName),
                    account.AccountNumber,
                    GetAccountDisplayName(account.Name, account.CustomName, account.AccountNumber),
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

    public async Task<IReadOnlyList<PayBreakdownProfileDto>> GetPayBreakdownProfiles(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profiles = await dbContext.PayBreakdownProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ToPayBreakdownProfileDtos(tenantId, profiles, cancellationToken);
    }

    public async Task<PayBreakdownProfileDto?> GetPayBreakdownProfile(Guid profileId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = await dbContext.PayBreakdownProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        return (await ToPayBreakdownProfileDtos(tenantId, [profile], cancellationToken)).FirstOrDefault();
    }

    public async Task<PayBreakdownProfileDto> CreatePayBreakdownProfile(CreatePayBreakdownProfileRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var existingCount = await dbContext.PayBreakdownProfiles.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        if (existingCount >= 2)
        {
            throw new InvalidOperationException("Only two pay breakdown profiles can be created.");
        }

        await ValidatePayBreakdownAccounts(tenantId, request.MainAccountId, request.SavingsAccountId, cancellationToken);

        var profile = new PayBreakdownProfile
        {
            TenantId = tenantId,
            Name = CleanRequired(request.Name, "Profile name is required."),
            MainAccountId = request.MainAccountId,
            SavingsAccountId = request.SavingsAccountId,
            FortnightlyPayMinorUnits = Math.Max(0, request.FortnightlyPayMinorUnits),
            Currency = CleanCurrency(request.Currency ?? "AUD")
        };

        dbContext.PayBreakdownProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToPayBreakdownProfileDtos(tenantId, [profile], cancellationToken)).First();
    }

    public async Task<PayBreakdownProfileDto?> UpdatePayBreakdownProfile(Guid profileId, UpdatePayBreakdownProfileRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = await dbContext.PayBreakdownProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        await ValidatePayBreakdownAccounts(tenantId, request.MainAccountId, request.SavingsAccountId, cancellationToken);

        profile.Name = CleanRequired(request.Name, "Profile name is required.");
        profile.MainAccountId = request.MainAccountId;
        profile.SavingsAccountId = request.SavingsAccountId;
        profile.FortnightlyPayMinorUnits = Math.Max(0, request.FortnightlyPayMinorUnits);
        profile.Currency = CleanCurrency(request.Currency ?? profile.Currency);
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToPayBreakdownProfileDtos(tenantId, [profile], cancellationToken)).First();
    }

    public async Task<bool> DeletePayBreakdownProfile(Guid profileId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = await dbContext.PayBreakdownProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        dbContext.PayBreakdownProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<BudgetProfileDto>> GetBudgetProfiles(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profiles = await dbContext.BudgetProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ToBudgetProfileDtos(tenantId, profiles, cancellationToken);
    }

    public async Task<BudgetProfileDto> CreateBudgetProfile(CreateBudgetProfileRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = new BudgetProfile
        {
            TenantId = tenantId,
            Name = CleanRequired(request.Name, "Budget name is required."),
            WeeklyLimitMinorUnits = Math.Max(0, request.WeeklyLimitMinorUnits),
            Currency = CleanCurrency(request.Currency ?? "AUD"),
            CategoryMatchers = JsonSerializer.Serialize(CleanCategoryMatchers(request.CategoryMatchers))
        };

        dbContext.BudgetProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SetBudgetProfileTags(tenantId, profile.Id, request.TagIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToBudgetProfileDtos(tenantId, [profile], cancellationToken)).First();
    }

    public async Task<BudgetProfileDto?> UpdateBudgetProfile(Guid profileId, UpdateBudgetProfileRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = await dbContext.BudgetProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.Name = CleanRequired(request.Name, "Budget name is required.");
        profile.WeeklyLimitMinorUnits = Math.Max(0, request.WeeklyLimitMinorUnits);
        profile.Currency = CleanCurrency(request.Currency ?? profile.Currency);
        profile.CategoryMatchers = JsonSerializer.Serialize(CleanCategoryMatchers(request.CategoryMatchers));
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await SetBudgetProfileTags(tenantId, profile.Id, request.TagIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await ToBudgetProfileDtos(tenantId, [profile], cancellationToken)).First();
    }

    public async Task<bool> DeleteBudgetProfile(Guid profileId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var profile = await dbContext.BudgetProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        await dbContext.BudgetProfileTags.Where(x => x.TenantId == tenantId && x.BudgetProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        dbContext.BudgetProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TransactionTagDto>> GetTags(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        await DefaultBankingData.EnsureDefaultTags(tenantId, dbContext, cancellationToken);
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
        await dbContext.BudgetProfileTags.Where(x => x.TenantId == tenantId && x.TransactionTagId == tagId).ExecuteDeleteAsync(cancellationToken);
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
        await DefaultBankingData.EnsureDefaultTags(tenantId, dbContext, cancellationToken);
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

        var merchantKey = DefaultBankingData.GetMerchantKey(merchantName);
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
            MerchantKey = NormalizeKey(request.MerchantName),
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
        subscription.MerchantKey = NormalizeKey(request.MerchantName);
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
        return NormalizeKey(GetDisplayMerchantName(merchantName, description));
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

    private static string GetAccountReferenceName(string name, string customName)
    {
        return string.IsNullOrWhiteSpace(customName) ? name : customName;
    }

    private static string GetAccountDisplayName(string name, string customName, string accountNumber)
    {
        var referenceName = GetAccountReferenceName(name, customName);
        return string.IsNullOrWhiteSpace(accountNumber) ? referenceName : $"{referenceName} - {accountNumber}";
    }

    private static IReadOnlyList<OverviewDailyCashFlowAccumulator> GetDailyCashFlowDays(DateOnly today, string? range)
    {
        var start = GetDailyCashFlowStart(today, range);

        return Enumerable.Range(0, today.DayNumber - start.DayNumber + 1)
            .Select(x => start.AddDays(x))
            .Select(x => new OverviewDailyCashFlowAccumulator(x.ToString("yyyy-MM-dd"), x.Day))
            .ToList();
    }

    private static IReadOnlyList<OverviewDailyCashFlowDto> GetDailyCashFlowDtos(
        IReadOnlyList<OverviewTransactionRow> transactions,
        IReadOnlyDictionary<Guid, List<TransactionTagDto>> tagsByTransaction,
        bool includeInternalTransfers,
        string internalTransferTagName,
        string? range)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyCashFlow = GetDailyCashFlowDays(today, range);
        var dailyCashFlowMap = dailyCashFlow.ToDictionary(x => x.Key);
        var internalTagName = internalTransferTagName.ToLowerInvariant();

        foreach (var transaction in transactions)
        {
            var tags = tagsByTransaction.GetValueOrDefault(transaction.Id, []);
            if (!includeInternalTransfers && tags.Any(x => x.Name.ToLowerInvariant() == internalTagName))
            {
                continue;
            }

            if (!dailyCashFlowMap.TryGetValue(transaction.PostedDate.ToString("yyyy-MM-dd"), out var day))
            {
                continue;
            }

            if (transaction.AmountMinorUnits > 0)
            {
                day.IncomeMinorUnits += transaction.AmountMinorUnits;
            }

            if (transaction.AmountMinorUnits < 0)
            {
                day.ExpensesMinorUnits += Math.Abs(transaction.AmountMinorUnits);
            }
        }

        return dailyCashFlow.Select(x => new OverviewDailyCashFlowDto(x.Key, x.Day, x.IncomeMinorUnits, x.ExpensesMinorUnits)).ToList();
    }

    private static DateOnly GetDailyCashFlowStart(DateOnly today, string? range)
    {
        return CleanDailyCashFlowRange(range) switch
        {
            "1w" => today.AddDays(-6),
            "3m" => today.AddMonths(-3).AddDays(1),
            _ => today.AddMonths(-1).AddDays(1)
        };
    }

    private static string CleanDailyCashFlowRange(string? range)
    {
        return range?.Trim().ToLowerInvariant() switch
        {
            "1w" => "1w",
            "3m" => "3m",
            _ => "1m"
        };
    }

    private static bool IsInterestTransaction(string description)
    {
        return description.Equals("Bonus Interest", StringComparison.OrdinalIgnoreCase)
            || description.Equals("Credit Interest", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<Guid> GetTemporarySavingsDepositIds(IReadOnlyList<SavingsTrajectoryTransactionRow> rows)
    {
        var withdrawals = rows
            .Where(x => x.AmountMinorUnits < 0)
            .OrderBy(x => x.PostedDate)
            .ToList();
        var matchedWithdrawalIds = new HashSet<Guid>();
        var temporaryDepositIds = new HashSet<Guid>();

        foreach (var deposit in rows.Where(x => x.AmountMinorUnits > 0 && !IsInterestTransaction(x.Description)).OrderBy(x => x.PostedDate))
        {
            var matchingWithdrawal = withdrawals.FirstOrDefault(x =>
                !matchedWithdrawalIds.Contains(x.Id)
                && Math.Abs(x.AmountMinorUnits) == deposit.AmountMinorUnits
                && x.PostedDate >= deposit.PostedDate
                && x.PostedDate <= deposit.PostedDate.AddDays(90));
            if (matchingWithdrawal is null)
            {
                continue;
            }

            matchedWithdrawalIds.Add(matchingWithdrawal.Id);
            temporaryDepositIds.Add(deposit.Id);
        }

        return temporaryDepositIds;
    }

    private static int GetElapsedDaysInMonth(string monthKey)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = int.Parse(monthKey[..4]);
        var month = int.Parse(monthKey[5..7]);
        if (today.Year == year && today.Month == month)
        {
            return today.Day;
        }

        return DateTime.DaysInMonth(year, month);
    }

    private async Task UpsertAverageDailySpendSnapshot(Guid tenantId, Guid? accountId, bool includeInternalTransfers, DateOnly snapshotDate, long averageDailySpend, CancellationToken cancellationToken)
    {
        var scopeKey = GetOverviewMetricScopeKey(accountId, includeInternalTransfers);
        var snapshot = await dbContext.OverviewMetricSnapshots
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ScopeKey == scopeKey && x.SnapshotDate == snapshotDate, cancellationToken);

        if (snapshot is null)
        {
            dbContext.OverviewMetricSnapshots.Add(new OverviewMetricSnapshot
            {
                TenantId = tenantId,
                BankAccountId = accountId,
                ScopeKey = scopeKey,
                SnapshotDate = snapshotDate,
                AverageDailySpendMinorUnits = averageDailySpend
            });
        }
        else
        {
            snapshot.AverageDailySpendMinorUnits = averageDailySpend;
            snapshot.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<DateOnly, long>> CalculateAverageDailySpendHistory(
        Guid tenantId,
        IReadOnlyList<Guid> accountIds,
        bool includeInternalTransfers,
        DateOnly from,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(from.Year, from.Month, 1);
        var transactions = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId
                && accountIds.Contains(x.BankAccountId)
                && x.Status == "posted"
                && x.PostedDate >= monthStart
                && x.PostedDate <= today
                && x.AmountMinorUnits < 0)
            .Select(x => new OverviewTransactionRow(x.Id, x.AmountMinorUnits, x.PostedDate))
            .ToListAsync(cancellationToken);

        if (!includeInternalTransfers)
        {
            var transactionIds = transactions.Select(x => x.Id).ToList();
            var internalTagName = DefaultBankingData.InternalTransferTagName.ToLowerInvariant();
            var internalTransferTransactionIds = await dbContext.BankTransactionTags
                .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
                .Join(dbContext.TransactionTags.Where(x => x.TenantId == tenantId && x.Name.ToLower() == internalTagName),
                    x => x.TransactionTagId,
                    y => y.Id,
                    (x, y) => x.BankTransactionId)
                .ToListAsync(cancellationToken);
            var internalTransferTransactionIdSet = internalTransferTransactionIds.ToHashSet();
            transactions = transactions.Where(x => !internalTransferTransactionIdSet.Contains(x.Id)).ToList();
        }

        var spendByDate = transactions
            .GroupBy(x => x.PostedDate)
            .ToDictionary(x => x.Key, x => x.Sum(y => Math.Abs(y.AmountMinorUnits)));
        var values = new Dictionary<DateOnly, long>();
        long runningMonthSpend = 0;
        DateOnly? currentMonth = null;

        foreach (var date in Enumerable.Range(0, today.DayNumber - monthStart.DayNumber + 1).Select(x => monthStart.AddDays(x)))
        {
            var dateMonth = new DateOnly(date.Year, date.Month, 1);
            if (currentMonth != dateMonth)
            {
                currentMonth = dateMonth;
                runningMonthSpend = 0;
            }

            runningMonthSpend += spendByDate.GetValueOrDefault(date);
            if (date >= from)
            {
                values[date] = runningMonthSpend / date.Day;
            }
        }

        return values;
    }

    private async Task<IReadOnlyList<BudgetProfileDto>> ToBudgetProfileDtos(Guid tenantId, IReadOnlyList<BudgetProfile> profiles, CancellationToken cancellationToken)
    {
        if (profiles.Count == 0)
        {
            return [];
        }

        var profileIds = profiles.Select(x => x.Id).ToList();
        var profileTags = await dbContext.BudgetProfileTags
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && profileIds.Contains(x.BudgetProfileId))
            .Join(dbContext.TransactionTags.AsNoTracking().Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BudgetProfileId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BudgetProfileId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentWeekStart = GetWeekStart(today);
        var from = currentWeekStart.AddDays(-7 * 11);
        var to = currentWeekStart.AddDays(6);
        var transactions = await dbContext.BankTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.Status == "posted"
                && x.PostedDate >= from
                && x.PostedDate <= to
                && x.AmountMinorUnits < 0)
            .Select(x => new BudgetTransactionRow(x.Id, x.Description, x.MerchantName, x.Category, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
        var transactionIds = transactions.Select(x => x.Id).ToList();
        var tagsByTransaction = await dbContext.BankTransactionTags
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.AsNoTracking().Where(x => x.TenantId == tenantId),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => new { x.BankTransactionId, Tag = new TransactionTagDto(y.Id, y.Name, y.Color) })
            .GroupBy(x => x.BankTransactionId)
            .ToDictionaryAsync(x => x.Key, x => x.Select(y => y.Tag).OrderBy(y => y.Name).ToList(), cancellationToken);

        return profiles
            .Select(x =>
            {
                var categoryMatchers = ParseCategoryMatchers(x.CategoryMatchers);
                var tags = profileTags.GetValueOrDefault(x.Id, []);
                var weeks = GetBudgetWeeks(x.WeeklyLimitMinorUnits, categoryMatchers, tags.Select(y => y.Id).ToHashSet(), transactions, tagsByTransaction, currentWeekStart);

                return new BudgetProfileDto(
                    x.Id,
                    x.Name,
                    x.WeeklyLimitMinorUnits,
                    x.Currency,
                    categoryMatchers,
                    tags,
                    weeks.First(),
                    weeks.Skip(1).ToList());
            })
            .ToList();
    }

    private static IReadOnlyList<BudgetWeekDto> GetBudgetWeeks(
        long weeklyLimitMinorUnits,
        IReadOnlyList<string> categoryMatchers,
        HashSet<Guid> tagIds,
        IReadOnlyList<BudgetTransactionRow> transactions,
        IReadOnlyDictionary<Guid, List<TransactionTagDto>> tagsByTransaction,
        DateOnly currentWeekStart)
    {
        return Enumerable.Range(0, 12)
            .Select(x => currentWeekStart.AddDays(-7 * x))
            .Select(x =>
            {
                var weekTransactions = transactions
                    .Where(y => y.PostedDate >= x && y.PostedDate <= x.AddDays(6) && IsBudgetTransaction(y, tagsByTransaction.GetValueOrDefault(y.Id, []), categoryMatchers, tagIds))
                    .OrderByDescending(y => y.PostedDate)
                    .Select(y => new BudgetTransactionDto(
                        y.Id,
                        y.Description,
                        y.MerchantName,
                        y.Category,
                        Math.Abs(y.AmountMinorUnits),
                        y.Currency,
                        y.PostedDate,
                        tagsByTransaction.GetValueOrDefault(y.Id, [])))
                    .ToList();
                var spent = weekTransactions.Sum(y => y.AmountMinorUnits);
                var usedPercent = weeklyLimitMinorUnits > 0 ? Math.Round(spent / (decimal)weeklyLimitMinorUnits * 100, 1) : 0;

                return new BudgetWeekDto(x, x.AddDays(6), spent, weeklyLimitMinorUnits - spent, usedPercent, weekTransactions);
            })
            .ToList();
    }

    private static bool IsBudgetTransaction(BudgetTransactionRow transaction, IReadOnlyList<TransactionTagDto> tags, IReadOnlyList<string> categoryMatchers, HashSet<Guid> tagIds)
    {
        return categoryMatchers.Any(x => transaction.Category.Contains(x, StringComparison.OrdinalIgnoreCase))
            || tags.Any(x => tagIds.Contains(x.Id));
    }

    private async Task SetBudgetProfileTags(Guid tenantId, Guid profileId, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken)
    {
        var validTagIds = await dbContext.TransactionTags
            .Where(x => x.TenantId == tenantId && tagIds.Distinct().Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        await dbContext.BudgetProfileTags.Where(x => x.TenantId == tenantId && x.BudgetProfileId == profileId).ExecuteDeleteAsync(cancellationToken);

        foreach (var tagId in validTagIds)
        {
            dbContext.BudgetProfileTags.Add(new BudgetProfileTag { TenantId = tenantId, BudgetProfileId = profileId, TransactionTagId = tagId });
        }
    }

    private static IReadOnlyList<string> CleanCategoryMatchers(IReadOnlyList<string> categoryMatchers)
    {
        return categoryMatchers
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static IReadOnlyList<string> ParseCategoryMatchers(string categoryMatchers)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(categoryMatchers) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private async Task ValidatePayBreakdownAccounts(Guid tenantId, Guid mainAccountId, Guid? savingsAccountId, CancellationToken cancellationToken)
    {
        if (savingsAccountId == mainAccountId)
        {
            throw new InvalidOperationException("Savings account must be different to the main account.");
        }

        var requiredAccountIds = new[] { mainAccountId }
            .Concat(savingsAccountId is { } x ? [x] : [])
            .ToList();
        var existingAccountIds = await dbContext.BankAccounts
            .Where(x => x.TenantId == tenantId && requiredAccountIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingAccountIds.Count != requiredAccountIds.Count)
        {
            throw new InvalidOperationException("Selected accounts must belong to the current tenant.");
        }
    }

    private async Task<IReadOnlyList<PayBreakdownProfileDto>> ToPayBreakdownProfileDtos(Guid tenantId, IReadOnlyList<PayBreakdownProfile> profiles, CancellationToken cancellationToken)
    {
        if (profiles.Count == 0)
        {
            return [];
        }

        var accountIds = profiles.Select(x => x.MainAccountId)
            .Concat(profiles.Where(x => x.SavingsAccountId is not null).Select(x => x.SavingsAccountId!.Value))
            .Distinct()
            .ToList();
        var accounts = (await GetAccountDtos(tenantId, accountId: null, cancellationToken))
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var breakdowns = new Dictionary<Guid, PayBreakdownDto>();

        foreach (var profile in profiles)
        {
            breakdowns[profile.Id] = await GetPayBreakdown(tenantId, profile, cancellationToken);
        }

        return profiles
            .Where(x => accounts.ContainsKey(x.MainAccountId))
            .Select(x => new PayBreakdownProfileDto(
                x.Id,
                x.Name,
                accounts[x.MainAccountId],
                x.SavingsAccountId is { } savingsAccountId ? accounts.GetValueOrDefault(savingsAccountId) : null,
                x.FortnightlyPayMinorUnits,
                x.Currency,
                x.CreatedAt,
                x.UpdatedAt,
                breakdowns[x.Id]))
            .ToList();
    }

    private async Task<PayBreakdownDto> GetPayBreakdown(Guid tenantId, PayBreakdownProfile profile, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payDate = await GetLatestPayDate(tenantId, profile, today, cancellationToken);
        var from = payDate ?? today.AddDays(-13);
        var transactions = await dbContext.BankTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BankAccountId == profile.MainAccountId
                && x.Status == "posted"
                && x.PostedDate >= from
                && x.PostedDate <= today
                && x.AmountMinorUnits < 0)
            .Select(x => new PayBreakdownTransactionRow(x.Id, x.Description, x.MerchantName, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
        var transactionIds = transactions.Select(x => x.Id).ToList();
        var internalTagName = DefaultBankingData.InternalTransferTagName.ToLowerInvariant();
        var internalTransactionIds = await dbContext.BankTransactionTags
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && transactionIds.Contains(x.BankTransactionId))
            .Join(dbContext.TransactionTags.AsNoTracking().Where(x => x.TenantId == tenantId && x.Name.ToLower() == internalTagName),
                x => x.TransactionTagId,
                y => y.Id,
                (x, y) => x.BankTransactionId)
            .ToHashSetAsync(cancellationToken);
        var savingsTransferIds = await GetSavingsTransferTransactionIds(tenantId, profile.SavingsAccountId, transactions, cancellationToken);

        var savingsTransferTransactions = transactions.Where(x => savingsTransferIds.Contains(x.Id)).OrderByDescending(x => x.PostedDate).ToList();
        var internalExpenseTransactions = transactions.Where(x => !savingsTransferIds.Contains(x.Id) && internalTransactionIds.Contains(x.Id)).OrderByDescending(x => x.PostedDate).ToList();
        var personalExpenseTransactions = transactions.Where(x => !savingsTransferIds.Contains(x.Id) && !internalTransactionIds.Contains(x.Id)).OrderByDescending(x => x.PostedDate).ToList();
        var savingsTransfer = savingsTransferTransactions.Sum(x => Math.Abs(x.AmountMinorUnits));
        var internalExpense = internalExpenseTransactions.Sum(x => Math.Abs(x.AmountMinorUnits));
        var personalExpense = personalExpenseTransactions.Sum(x => Math.Abs(x.AmountMinorUnits));
        var remaining = profile.FortnightlyPayMinorUnits - personalExpense - internalExpense - savingsTransfer;

        return new PayBreakdownDto(
            from,
            today,
            payDate is not null,
            profile.FortnightlyPayMinorUnits,
            personalExpense,
            internalExpense,
            savingsTransfer,
            remaining,
            [
                new PayBreakdownCategoryDto("personal", "Personal expense", personalExpense, ToPayBreakdownTransactions(personalExpenseTransactions)),
                new PayBreakdownCategoryDto("internal", "Internal expense", internalExpense, ToPayBreakdownTransactions(internalExpenseTransactions)),
                new PayBreakdownCategoryDto("savings", "Savings transfer", savingsTransfer, ToPayBreakdownTransactions(savingsTransferTransactions))
            ]);
    }

    private static IReadOnlyList<PayBreakdownTransactionDto> ToPayBreakdownTransactions(IReadOnlyList<PayBreakdownTransactionRow> transactions)
    {
        return transactions
            .Select(x => new PayBreakdownTransactionDto(x.Id, x.Description, x.MerchantName, Math.Abs(x.AmountMinorUnits), x.Currency, x.PostedDate))
            .ToList();
    }

    private async Task<DateOnly?> GetLatestPayDate(Guid tenantId, PayBreakdownProfile profile, DateOnly today, CancellationToken cancellationToken)
    {
        if (profile.FortnightlyPayMinorUnits <= 0)
        {
            return null;
        }

        var earliestPaySearchDate = today.AddMonths(-1);
        var tolerance = Math.Max(100, (long)Math.Round(profile.FortnightlyPayMinorUnits * 0.02));
        return await dbContext.BankTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BankAccountId == profile.MainAccountId
                && x.Status == "posted"
                && x.PostedDate >= earliestPaySearchDate
                && x.PostedDate <= today
                && x.AmountMinorUnits > 0
                && Math.Abs(x.AmountMinorUnits - profile.FortnightlyPayMinorUnits) <= tolerance)
            .OrderByDescending(x => x.PostedDate)
            .Select(x => (DateOnly?)x.PostedDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetSavingsTransferTransactionIds(Guid tenantId, Guid? savingsAccountId, IReadOnlyList<PayBreakdownTransactionRow> transactions, CancellationToken cancellationToken)
    {
        if (savingsAccountId is null || transactions.Count == 0)
        {
            return [];
        }

        var from = transactions.Min(x => x.PostedDate);
        var to = transactions.Max(x => x.PostedDate);
        var savingsDeposits = await dbContext.BankTransactions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BankAccountId == savingsAccountId.Value
                && x.Status == "posted"
                && x.PostedDate >= from.AddDays(-1)
                && x.PostedDate <= to.AddDays(1)
                && x.AmountMinorUnits > 0)
            .Select(x => new PayBreakdownTransactionRow(x.Id, x.Description, x.MerchantName, x.AmountMinorUnits, x.Currency, x.PostedDate))
            .ToListAsync(cancellationToken);
        var matchedDepositIds = new HashSet<Guid>();
        var savingsTransferIds = new HashSet<Guid>();

        foreach (var transaction in transactions.OrderBy(x => x.PostedDate))
        {
            var matchingDeposit = savingsDeposits.FirstOrDefault(x =>
                !matchedDepositIds.Contains(x.Id)
                && Math.Abs(x.AmountMinorUnits) == Math.Abs(transaction.AmountMinorUnits)
                && Math.Abs(x.PostedDate.DayNumber - transaction.PostedDate.DayNumber) <= 1);
            if (matchingDeposit is null)
            {
                continue;
            }

            matchedDepositIds.Add(matchingDeposit.Id);
            savingsTransferIds.Add(transaction.Id);
        }

        return savingsTransferIds;
    }

    private static bool ShouldIncludeInternalTransfers(Guid? accountId, bool? includeInternalTransfers)
    {
        return accountId is not null && (includeInternalTransfers ?? true);
    }

    private static string GetOverviewMetricScopeKey(Guid? accountId, bool includeInternalTransfers)
    {
        if (accountId is null)
        {
            return "all";
        }

        return includeInternalTransfers ? accountId.Value.ToString("D") : $"{accountId.Value:D}:exclude-internal";
    }

    private static string FormatMonthLabel(string monthKey)
    {
        var date = new DateTime(int.Parse(monthKey[..4]), int.Parse(monthKey[5..7]), 1);
        return date.ToString("MMM");
    }

    private static string FormatFullMonthLabel(string monthKey)
    {
        var date = new DateTime(int.Parse(monthKey[..4]), int.Parse(monthKey[5..7]), 1);
        return date.ToString("MMMM yyyy");
    }

    private static string GetTimeframeLabel(IReadOnlyList<OverviewMonthAccumulator> months)
    {
        var firstMonth = months.FirstOrDefault();
        var lastMonth = months.LastOrDefault();
        if (firstMonth is null || lastMonth is null)
        {
            return "No posted spending data yet";
        }

        return firstMonth.Key == lastMonth.Key
            ? FormatFullMonthLabel(firstMonth.Key)
            : $"{FormatFullMonthLabel(firstMonth.Key)} to {FormatFullMonthLabel(lastMonth.Key)}";
    }

    private async Task ApplyMerchantTag(Guid tenantId, string merchantKey, Guid tagId, CancellationToken cancellationToken)
    {
        var merchantRows = await dbContext.BankTransactions
            .Where(x => x.TenantId == tenantId && x.MerchantName != null)
            .Select(x => new { x.Id, x.MerchantName })
            .ToListAsync(cancellationToken);
        var transactionIds = merchantRows
            .Where(x => x.MerchantName is not null && DefaultBankingData.GetMerchantKey(x.MerchantName) == merchantKey)
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

    private sealed record OverviewTransactionRow(Guid Id, long AmountMinorUnits, DateOnly PostedDate);

    private sealed record SavingsTrajectoryTransactionRow(Guid Id, long AmountMinorUnits, DateOnly PostedDate, string Description);

    private sealed record SavingsTrajectoryDay(long DepositMinorUnits, long InterestMinorUnits, long WithdrawalMinorUnits);

    private sealed record PayBreakdownTransactionRow(Guid Id, string Description, string? MerchantName, long AmountMinorUnits, string Currency, DateOnly PostedDate);

    private sealed record BudgetTransactionRow(Guid Id, string Description, string? MerchantName, string Category, long AmountMinorUnits, string Currency, DateOnly PostedDate);

    private sealed class OverviewMonthAccumulator(string key, string label)
    {
        public string Key { get; } = key;

        public string Label { get; } = label;

        public long TotalMinorUnits { get; set; }

        public Dictionary<Guid, long> Tags { get; } = [];
    }

    private sealed class OverviewTagAccumulator(Guid id, string name, string color, List<long> months)
    {
        public Guid Id { get; } = id;

        public string Name { get; } = name;

        public string Color { get; } = color;

        public long TotalMinorUnits { get; set; }

        public List<long> Months { get; } = months;
    }

    private sealed class OverviewDailyCashFlowAccumulator(string key, int day)
    {
        public string Key { get; } = key;

        public int Day { get; } = day;

        public long IncomeMinorUnits { get; set; }

        public long ExpensesMinorUnits { get; set; }
    }

    private sealed record AccountDisplay(string Name, string CustomName, string AccountNumber);

    private sealed record AccountRow(Guid Id, Guid BankConnectionId, string Name, string CustomName, string AccountNumber, string Currency);

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
