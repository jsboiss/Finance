import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AccountsList } from '../components/AccountsList'
import { Header } from '../components/Header'
import { api } from '../lib/api'
import type { Account } from '../lib/types'

export function Accounts() {
  const queryClient = useQueryClient()
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const updateAccount = useMutation({
    mutationFn: ({ accountId, customName }: { accountId: string; customName: string }) => api<Account>(`/api/accounts/${accountId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ customName })
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
      queryClient.invalidateQueries({ queryKey: ['transactions'] })
    }
  })

  return (
    <section className="space-y-6">
      <Header title="Accounts" subtitle="Connected Redbark accounts and current balances." />
      <AccountsList accounts={accounts.data ?? []} isLoading={accounts.isLoading} isSaving={updateAccount.isPending} onRename={(accountId, customName) => updateAccount.mutate({ accountId, customName })} />
    </section>
  )
}
