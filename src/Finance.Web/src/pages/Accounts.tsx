import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AccountsList } from '../components/AccountsList'
import { Header } from '../components/Header'
import { Button } from '../components/ui/button'
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
  const discoverAccounts = useMutation({
    mutationFn: () => api<void>('/api/operations/accounts/discover', { method: 'POST' }),
    onSuccess: () => invalidateAccountData(queryClient)
  })
  const syncAccount = useMutation({
    mutationFn: (accountId: string) => api<void>(`/api/operations/accounts/${accountId}/backfill`, { method: 'POST' }),
    onSuccess: () => invalidateAccountData(queryClient)
  })
  const clearAccount = useMutation({
    mutationFn: (accountId: string) => api<void>(`/api/operations/accounts/${accountId}/data`, { method: 'DELETE' }),
    onSuccess: () => invalidateAccountData(queryClient)
  })

  const isOperating = discoverAccounts.isPending || syncAccount.isPending || clearAccount.isPending

  function onClearAccount(accountId: string, displayName: string) {
    if (window.confirm(`Clear imported transactions and balances for ${displayName}?`)) {
      clearAccount.mutate(accountId)
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Accounts" subtitle="Connected Redbark accounts and current balances." />
      <div className="flex flex-wrap gap-2">
        <Button disabled={isOperating} onClick={() => discoverAccounts.mutate()}>
          Discover accounts
        </Button>
      </div>
      {(discoverAccounts.error || syncAccount.error || clearAccount.error) && (
        <p className="text-sm text-destructive">Operation failed. Check the latest import card or Railway logs for details.</p>
      )}
      <AccountsList
        accounts={accounts.data ?? []}
        isLoading={accounts.isLoading}
        isOperating={isOperating}
        isSaving={updateAccount.isPending}
        onClear={onClearAccount}
        onRename={(accountId, customName) => updateAccount.mutate({ accountId, customName })}
        onSync={accountId => syncAccount.mutate(accountId)}
      />
    </section>
  )
}

function invalidateAccountData(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['accounts'] })
  void queryClient.invalidateQueries({ queryKey: ['balances'] })
  void queryClient.invalidateQueries({ queryKey: ['transactions'] })
  void queryClient.invalidateQueries({ queryKey: ['imports'] })
  void queryClient.invalidateQueries({ queryKey: ['operations-status'] })
}
