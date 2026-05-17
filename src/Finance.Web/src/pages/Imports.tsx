import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Activity, DatabaseZap, RefreshCcw, RotateCcw, Trash2 } from 'lucide-react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ImportRun, OperationsStatus } from '../lib/types'

export function Imports() {
  const queryClient = useQueryClient()
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports'), refetchInterval: x => hasRunningImport(x.state.data) ? 3000 : false })
  const status = useQuery({ queryKey: ['operations-status'], queryFn: () => api<OperationsStatus>('/api/operations/status'), refetchInterval: imports.data?.some(x => x.status === 'running') ? 3000 : false })
  const discoverAccounts = useOperation('/api/operations/accounts/discover', queryClient)
  const backfill = useOperation('/api/operations/backfill', queryClient)
  const reconcile = useOperation('/api/operations/reconcile', queryClient)
  const reconcileFull = useOperation('/api/operations/reconcile/full', queryClient)
  const clearData = useMutation({
    mutationFn: () => api<void>('/api/operations/data', { method: 'DELETE' }),
    onSuccess: () => invalidateOperationalData(queryClient)
  })

  const isRunning = discoverAccounts.isPending || backfill.isPending || reconcile.isPending || reconcileFull.isPending || clearData.isPending
  const activeOperation = getActiveOperation([
    [discoverAccounts.isPending, 'Discovering accounts'],
    [backfill.isPending, 'Running data sync'],
    [reconcile.isPending, 'Running recent recon'],
    [reconcileFull.isPending, 'Running full recon'],
    [clearData.isPending, 'Clearing data']
  ])

  function onClearData() {
    if (window.confirm('Clear all imported banking data, import history, and Redbark request counters?')) {
      clearData.mutate()
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Imports" subtitle="Backfill, webhook, and reconciliation run status." />
      <div className="grid gap-3 md:grid-cols-4">
        <RequestMetric label="Today" value={status.data?.redbarkRequestsToday} />
        <RequestMetric label="This month" value={status.data?.redbarkRequestsThisMonth} />
        <RequestMetric label="Total" value={status.data?.redbarkRequestsTotal} />
        <RequestMetric label="Last request" value={status.data?.lastRedbarkRequestAt ? new Date(status.data.lastRedbarkRequestAt).toLocaleString() : 'Never'} />
      </div>
      {(activeOperation || imports.data?.some(x => x.status === 'running')) && (
        <div className="flex items-center gap-2 rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground">
          <Activity className="size-4 animate-pulse text-foreground" />
          <span>{activeOperation ?? 'Sync is still running'}. This page refreshes status automatically.</span>
        </div>
      )}
      <div className="flex flex-wrap gap-2">
        <Button disabled={isRunning} onClick={() => reconcile.mutate()}>
          <RefreshCcw data-icon="inline-start" />
          {reconcile.isPending ? 'Running recent recon' : 'Run recent recon'}
        </Button>
        <Button disabled={isRunning} onClick={() => discoverAccounts.mutate()} variant="secondary">
          <DatabaseZap data-icon="inline-start" />
          {discoverAccounts.isPending ? 'Discovering accounts' : 'Discover accounts'}
        </Button>
        <Button disabled={isRunning} onClick={() => reconcileFull.mutate()} variant="secondary">
          <RotateCcw data-icon="inline-start" />
          {reconcileFull.isPending ? 'Running full recon' : 'Run full recon'}
        </Button>
        <Button disabled={isRunning} onClick={() => backfill.mutate()} variant="outline">
          <DatabaseZap data-icon="inline-start" />
          {backfill.isPending ? 'Running data sync' : 'Run data sync'}
        </Button>
        <Button disabled={isRunning} onClick={onClearData} variant="destructive">
          <Trash2 data-icon="inline-start" />
          Clear data
        </Button>
      </div>
      {(discoverAccounts.error || backfill.error || reconcile.error || reconcileFull.error || clearData.error) && (
        <p className="text-sm text-destructive">Operation failed. Check the API logs for the Redbark error details.</p>
      )}
      <div className="grid gap-3">
        {(imports.data ?? []).map(x => (
          <Card className="p-4" key={x.id}>
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="font-medium">{x.source}</p>
                <p className="text-sm text-muted-foreground">{new Date(x.startedAt).toLocaleString()}</p>
              </div>
            <Badge variant={x.status === 'failed' ? 'destructive' : 'secondary'}>{x.status}</Badge>
            </div>
            <p className="mt-3 text-sm text-muted-foreground">{x.importedCount} transactions imported</p>
            {x.error && <p className="mt-3 text-sm text-destructive">{x.error}</p>}
          </Card>
        ))}
        {!imports.isLoading && imports.data?.length === 0 && <p className="text-sm text-muted-foreground">No import runs yet.</p>}
      </div>
    </section>
  )
}

function hasRunningImport(imports: ImportRun[] | undefined) {
  return imports?.some(x => x.status === 'running') ?? false
}

function getActiveOperation(operations: Array<[boolean, string]>) {
  return operations.find(x => x[0])?.[1]
}

function useOperation(path: string, queryClient: ReturnType<typeof useQueryClient>) {
  return useMutation({
    mutationFn: () => api<void>(path, { method: 'POST' }),
    onSuccess: () => invalidateOperationalData(queryClient)
  })
}

function invalidateOperationalData(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ['imports'] })
  void queryClient.invalidateQueries({ queryKey: ['operations-status'] })
  void queryClient.invalidateQueries({ queryKey: ['accounts'] })
  void queryClient.invalidateQueries({ queryKey: ['transactions'] })
}

function RequestMetric({ label, value }: { label: string; value?: number | string }) {
  return (
    <Card className="p-4">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-1 text-2xl font-semibold">{value ?? '-'}</p>
    </Card>
  )
}
