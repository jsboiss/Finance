import { useQuery } from '@tanstack/react-query'
import { Header } from '../components/Header'
import { api } from '../lib/api'
import type { ImportRun } from '../lib/types'

export function Imports() {
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports') })

  return (
    <section className="space-y-6">
      <Header title="Imports" subtitle="Backfill, webhook, and reconciliation run status." />
      <div className="grid gap-3">
        {(imports.data ?? []).map(x => (
          <div className="rounded-lg border border-zinc-200 bg-white p-4" key={x.id}>
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="font-medium">{x.source}</p>
                <p className="text-sm text-zinc-500">{new Date(x.startedAt).toLocaleString()}</p>
              </div>
              <span className="rounded-full bg-zinc-100 px-3 py-1 text-xs font-medium">{x.status}</span>
            </div>
            {x.error && <p className="mt-3 text-sm text-red-700">{x.error}</p>}
          </div>
        ))}
        {!imports.isLoading && imports.data?.length === 0 && <p className="text-sm text-zinc-500">No import runs yet.</p>}
      </div>
    </section>
  )
}
