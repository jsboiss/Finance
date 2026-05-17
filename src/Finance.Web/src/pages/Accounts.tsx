import { useQuery } from '@tanstack/react-query'
import { AccountsList } from '../components/AccountsList'
import { Header } from '../components/Header'
import { api } from '../lib/api'
import type { Account } from '../lib/types'

export function Accounts() {
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })

  return (
    <section className="space-y-6">
      <Header title="Accounts" subtitle="Connected Redbark accounts and current balances." />
      <AccountsList accounts={accounts.data ?? []} isLoading={accounts.isLoading} />
    </section>
  )
}
