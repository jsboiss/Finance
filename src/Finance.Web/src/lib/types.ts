export type Account = {
  id: string
  name: string
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
