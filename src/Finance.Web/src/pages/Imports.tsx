import { useQuery } from '@tanstack/react-query'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ImportRun } from '../lib/types'

export function Imports() {
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports') })

  return (
    <section className="space-y-6">
      <Header title="Imports" subtitle="Backfill, webhook, and reconciliation run status." />
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
            {x.error && <p className="mt-3 text-sm text-red-700">{x.error}</p>}
          </Card>
        ))}
        {!imports.isLoading && imports.data?.length === 0 && <p className="text-sm text-muted-foreground">No import runs yet.</p>}
      </div>
    </section>
  )
}
