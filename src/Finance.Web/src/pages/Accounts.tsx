import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Activity, DatabaseZap } from 'lucide-react'
import { AccountsList } from '../components/AccountsList'
import { Header } from '../components/Header'
import { Button } from '../components/ui/button'
import { api } from '../lib/api'
import type { Account, ImportRun } from '../lib/types'

export function Accounts() {
  const queryClient = useQueryClient()
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports'), refetchInterval: x => hasRunningImport(x.state.data) ? 3000 : false })
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
  const activeOperation = getActiveOperation([
    [discoverAccounts.isPending, 'Discovering accounts'],
    [syncAccount.isPending, 'Syncing account'],
    [clearAccount.isPending, 'Clearing account data']
  ])

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
          <DatabaseZap data-icon="inline-start" />
          {discoverAccounts.isPending ? 'Discovering accounts' : 'Discover accounts'}
        </Button>
      </div>
      {(activeOperation || imports.data?.some(x => x.status === 'running')) && (
        <div className="flex items-center gap-2 rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground">
          <Activity className="size-4 animate-pulse text-foreground" />
          <span>{activeOperation ?? 'Sync is still running'}. Account data refreshes when the operation finishes.</span>
        </div>
      )}
      {imports.data?.slice(0, 3).map(x => (
        <div className="grid gap-1 rounded-md border border-border bg-card px-3 py-2 text-sm" key={x.id}>
          <div className="flex items-center justify-between gap-3">
            <span className="font-medium">{x.source}</span>
            <span className={x.status === 'failed' ? 'text-destructive' : 'text-muted-foreground'}>{x.status}</span>
          </div>
          <span className="text-muted-foreground">{x.importedCount} imported{x.completedAt ? `, completed ${new Date(x.completedAt).toLocaleString()}` : ''}</span>
          {x.error && <span className="text-destructive">{x.error}</span>}
        </div>
      ))}
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

function hasRunningImport(imports: ImportRun[] | undefined) {
  return imports?.some(x => x.status === 'running') ?? false
}

function getActiveOperation(operations: Array<[boolean, string]>) {
  return operations.find(x => x[0])?.[1]
}

function invalidateAccountData(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['accounts'] })
  void queryClient.invalidateQueries({ queryKey: ['balances'] })
  void queryClient.invalidateQueries({ queryKey: ['transactions'] })
  void queryClient.invalidateQueries({ queryKey: ['imports'] })
  void queryClient.invalidateQueries({ queryKey: ['operations-status'] })
}
