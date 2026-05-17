export type Account = {
  id: string
  name: string
  customName: string
  accountNumber: string
  displayName: string
  institutionName: string
  currency: string
  currentBalanceMinorUnits: number | null
}

export type Balance = {
  accountId: string
  currentMinorUnits: number | null
  currency: string
  asOf: string
}

export type Transaction = {
  id: string
  accountId: string
  externalTransactionId: string
  accountName: string
  accountNumber: string
  accountDisplayName: string
  description: string
  merchantName?: string
  merchantCategoryCode?: string
  category: string
  amountMinorUnits: number
  currency: string
  postedDate: string
  postedAt?: string
  direction: string
  status: string
  tags: TransactionTag[]
}

export type TransactionTag = {
  id: string
  name: string
  color: string
}

export type MerchantTagRule = {
  id: string
  merchantName: string
  tag: TransactionTag
}

export type ImportRun = {
  id: string
  source: string
  status: string
  startedAt: string
  completedAt?: string
  importedCount: number
  error?: string
}

export type OperationsStatus = {
  redbarkRequestsToday: number
  redbarkRequestsThisMonth: number
  redbarkRequestsTotal: number
  lastRedbarkRequestAt?: string
}

export type Overview = {
  balanceMinorUnits: number | null
  currentMonthSpendMinorUnits: number
  averageDailySpendMinorUnits: number
  taggedCoverage: number
  currentMonthKey: string
  currentMonthLabel: string
  timeframeLabel: string
  currentMonthIncomeMinorUnits: number
  months: OverviewMonthSpend[]
  topTags: OverviewTagSpend[]
  dailyCashFlow: OverviewDailyCashFlow[]
}

export type OverviewMonthSpend = {
  key: string
  label: string
  totalMinorUnits: number
  tags: OverviewMonthTagSpend[]
}

export type OverviewMonthTagSpend = {
  tagId: string
  amountMinorUnits: number
}

export type OverviewTagSpend = {
  id: string
  name: string
  color: string
  totalMinorUnits: number
  currentMinorUnits: number
  previousMinorUnits: number
  months: number[]
}

export type OverviewDailyCashFlow = {
  key: string
  day: number
  incomeMinorUnits: number
  expensesMinorUnits: number
}

export type Subscription = {
  id: string
  name: string
  merchantName: string
  merchantKey: string
  paymentManager: string
  cadence: string
  expectedAmountMinorUnits: number
  currency: string
  status: string
  statusOverride?: string
  isCancelled: boolean
  firstPaymentDate?: string
  lastPaymentDate?: string
  nextExpectedPaymentDate?: string
  totalPaidMinorUnits: number
  monthlyEstimateMinorUnits: number
  yearlyEstimateMinorUnits: number
  priceChanges: SubscriptionPriceChange[]
}

export type SubscriptionDetail = {
  subscription: Subscription
  payments: SubscriptionPayment[]
}

export type SubscriptionPayment = {
  transactionId: string
  description: string
  merchantName?: string
  amountMinorUnits: number
  currency: string
  postedDate: string
}

export type SubscriptionPriceChange = {
  effectiveDate: string
  previousAmountMinorUnits: number
  newAmountMinorUnits: number
  status: string
}

export type SubscriptionSuggestion = {
  id: string
  merchantName: string
  merchantKey: string
  paymentManager: string
  cadence: string
  expectedAmountMinorUnits: number
  currency: string
  confidence: number
  status: string
  firstPaymentDate: string
  lastPaymentDate: string
  nextExpectedPaymentDate: string
  sampleTransactionIds: string[]
}

export type ApiClient = {
  id: string
  tenantId: string
  tenantName: string
  name: string
  createdAt: string
  revokedAt?: string
}

export type Tenant = {
  id: string
  name: string
  createdAt: string
}

export type CreateApiClientResponse = {
  client: ApiClient
  apiKey: string
}
