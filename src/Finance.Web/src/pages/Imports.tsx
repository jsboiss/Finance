import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { DatabaseZap, RefreshCcw, RotateCcw, Trash2 } from 'lucide-react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ImportRun, OperationsStatus } from '../lib/types'

export function Imports() {
  const queryClient = useQueryClient()
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports') })
  const status = useQuery({ queryKey: ['operations-status'], queryFn: () => api<OperationsStatus>('/api/operations/status') })
  const backfill = useOperation('/api/operations/backfill', queryClient)
  const reconcile = useOperation('/api/operations/reconcile', queryClient)
  const reconcileFull = useOperation('/api/operations/reconcile/full', queryClient)
  const clearData = useMutation({
    mutationFn: () => api<void>('/api/operations/data', { method: 'DELETE' }),
    onSuccess: () => invalidateOperationalData(queryClient)
  })

  const isRunning = backfill.isPending || reconcile.isPending || reconcileFull.isPending || clearData.isPending

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
      <div className="flex flex-wrap gap-2">
        <Button disabled={isRunning} onClick={() => reconcile.mutate()}>
          <RefreshCcw data-icon="inline-start" />
          Run recent recon
        </Button>
        <Button disabled={isRunning} onClick={() => reconcileFull.mutate()} variant="secondary">
          <RotateCcw data-icon="inline-start" />
          Run full recon
        </Button>
        <Button disabled={isRunning} onClick={() => backfill.mutate()} variant="outline">
          <DatabaseZap data-icon="inline-start" />
          Run data sync
        </Button>
        <Button disabled={isRunning} onClick={onClearData} variant="destructive">
          <Trash2 data-icon="inline-start" />
          Clear data
        </Button>
      </div>
      {(backfill.error || reconcile.error || reconcileFull.error || clearData.error) && (
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
              <Badge variant="secondary">{x.status}</Badge>
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
