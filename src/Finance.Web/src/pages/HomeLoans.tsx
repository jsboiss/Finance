import { useQuery } from '@tanstack/react-query'
import { BanknoteArrowDown, Landmark, PiggyBank } from 'lucide-react'
import type { ReactNode } from 'react'
import { Header } from '../components/Header'
import { MetricGridLoading } from '../components/LoadingSkeletons'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Transaction } from '../lib/types'

export function HomeLoans() {
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const homeLoanAccounts = (accounts.data ?? []).filter(x => x.accountType === 'HomeLoan')
  const offsetAccounts = (accounts.data ?? []).filter(x => x.accountType === 'Offset')
  const trackedAccounts = [...homeLoanAccounts, ...offsetAccounts]
  const transactions = useQuery({
    enabled: trackedAccounts.length > 0,
    queryKey: ['home-loan-transactions', trackedAccounts.map(x => x.id).sort().join(',')],
    queryFn: async () => {
      const rows = await Promise.all(trackedAccounts.map(x => api<Transaction[]>(`/api/transactions?accountId=${x.id}&pageSize=25&sort=-date`)))
      return rows.flat().sort((x, y) => y.postedDate.localeCompare(x.postedDate)).slice(0, 50)
    }
  })

  const loanDebtMinorUnits = homeLoanAccounts.reduce((x, y) => x + Math.abs(y.currentBalanceMinorUnits ?? 0), 0)
  const offsetBalanceMinorUnits = offsetAccounts.reduce((x, y) => x + Math.max(y.currentBalanceMinorUnits ?? 0, 0), 0)
  const netDebtMinorUnits = Math.max(loanDebtMinorUnits - offsetBalanceMinorUnits, 0)
  const currencyCode = trackedAccounts[0]?.currency ?? 'AUD'

  return (
    <section className="space-y-6">
      <Header title="Home loans" subtitle="Loan and offset accounts are tracked separately from everyday cashflow." />

      {accounts.isLoading ? (
        <MetricGridLoading count={3} />
      ) : (
        <div className="grid gap-4 md:grid-cols-3">
          <SummaryMetric icon={<Landmark className="size-5" />} label="Loan balance" value={loanDebtMinorUnits} currencyCode={currencyCode} />
          <SummaryMetric icon={<PiggyBank className="size-5" />} label="Offset balance" value={offsetBalanceMinorUnits} currencyCode={currencyCode} />
          <SummaryMetric icon={<BanknoteArrowDown className="size-5" />} label="Net loan exposure" value={netDebtMinorUnits} currencyCode={currencyCode} />
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Linked accounts</CardTitle>
            <CardDescription>Set account types on the Accounts page to control what appears here.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {accounts.isLoading && <InlineListLoading />}
            {trackedAccounts.map(x => (
              <div className="grid gap-1 rounded-md border border-border p-3" key={x.id}>
                <div className="flex items-center justify-between gap-3">
                  <p className="font-medium">{x.displayName}</p>
                  <span className="rounded-md bg-muted px-2 py-1 text-xs text-muted-foreground">{formatAccountType(x.accountType)}</span>
                </div>
                <div className="flex items-center justify-between gap-3 text-sm text-muted-foreground">
                  <span>{x.institutionName}</span>
                  <span className="font-medium text-foreground">{currency(x.currentBalanceMinorUnits, x.currency)}</span>
                </div>
              </div>
            ))}
            {!accounts.isLoading && trackedAccounts.length === 0 && (
              <p className="text-sm text-muted-foreground">No home loan or offset accounts are classified yet.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent loan activity</CardTitle>
            <CardDescription>Transactions from home loan and offset accounts only.</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="overflow-hidden rounded-md border border-border">
              {transactions.isLoading && <InlineListLoading />}
              {(transactions.data ?? []).map(x => (
                <div className="grid gap-1 border-b border-border px-3 py-2 last:border-b-0 sm:grid-cols-[1fr_auto] sm:items-center" key={x.id}>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{x.description}</p>
                    <p className="truncate text-xs text-muted-foreground">{formatDate(x.postedDate)} - {x.accountDisplayName}</p>
                  </div>
                  <p className={x.amountMinorUnits < 0 ? 'text-sm font-semibold text-destructive sm:text-right' : 'text-sm font-semibold text-green-600 dark:text-green-400 sm:text-right'}>
                    {currency(x.amountMinorUnits, x.currency)}
                  </p>
                </div>
              ))}
              {!transactions.isLoading && (transactions.data?.length ?? 0) === 0 && (
                <p className="px-3 py-4 text-sm text-muted-foreground">No loan activity imported yet.</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>
    </section>
  )
}

function InlineListLoading() {
  return (
    <>
      {[0, 1, 2].map(x => (
        <div className="grid gap-1 border-b border-border px-3 py-2 last:border-b-0 sm:grid-cols-[1fr_auto] sm:items-center" key={x}>
          <div>
            <div className="h-4 w-48 max-w-full animate-pulse rounded bg-muted" />
            <div className="mt-2 h-3 w-36 max-w-full animate-pulse rounded bg-muted" />
          </div>
          <div className="h-5 w-24 animate-pulse rounded bg-muted sm:justify-self-end" />
        </div>
      ))}
    </>
  )
}

function SummaryMetric({ icon, label, value, currencyCode }: { icon: ReactNode; label: string; value: number; currencyCode: string }) {
  return (
    <Card>
      <CardContent className="flex items-center justify-between gap-4 p-4">
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 text-2xl font-semibold">{currency(value, currencyCode)}</p>
        </div>
        <div className="rounded-md border border-border bg-muted p-2 text-muted-foreground">{icon}</div>
      </CardContent>
    </Card>
  )
}

function formatAccountType(accountType: Account['accountType']) {
  return accountType === 'HomeLoan' ? 'Home loan' : accountType
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}
