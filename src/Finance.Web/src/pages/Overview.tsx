import { useQuery } from '@tanstack/react-query'
import { AccountsList } from '../components/AccountsList'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Balance } from '../lib/types'

export function Overview() {
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const balances = useQuery({ queryKey: ['balances'], queryFn: () => api<Balance[]>('/api/balances') })
  const availableBalances = accounts.data?.filter(x => x.currentBalanceMinorUnits != null) ?? []
  const total = availableBalances.length > 0 ? availableBalances.reduce((x, y) => x + (y.currentBalanceMinorUnits ?? 0), 0) : null

  return (
    <section className="space-y-6">
      <Header title="Overview" subtitle="Balances, posted activity, and ingestion health." />
      <div className="grid gap-4 md:grid-cols-3">
        <Metric label="Total balance" value={currency(total, 'AUD')} />
        <Metric label="Accounts" value={`${accounts.data?.length ?? 0}`} />
        <Metric label="Balance snapshots" value={`${balances.data?.length ?? 0}`} />
      </div>
      <AccountsList accounts={accounts.data ?? []} isLoading={accounts.isLoading} />
    </section>
  )
}
