import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Copy, KeyRound, Plus, ShieldOff } from 'lucide-react'
import { useState } from 'react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ApiClient, CreateApiClientResponse } from '../lib/types'

export function Settings() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('External UI')
  const [newApiKey, setNewApiKey] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const apiClients = useQuery({ queryKey: ['api-clients'], queryFn: () => api<ApiClient[]>('/api/api-clients') })
  const createApiClient = useMutation({
    mutationFn: () =>
      api<CreateApiClientResponse>('/api/api-clients', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name })
      }),
    onSuccess: x => {
      setNewApiKey(x.apiKey)
      setCopied(false)
      void queryClient.invalidateQueries({ queryKey: ['api-clients'] })
    }
  })
  const revokeApiClient = useMutation({
    mutationFn: (apiClientId: string) => api<void>(`/api/api-clients/${apiClientId}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['api-clients'] })
  })

  async function copyKey() {
    if (!newApiKey) {
      return
    }

    await navigator.clipboard.writeText(newApiKey)
    setCopied(true)
  }

  function revoke(apiClient: ApiClient) {
    if (window.confirm(`Revoke ${apiClient.name}? Existing integrations using this key will stop working.`)) {
      revokeApiClient.mutate(apiClient.id)
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Settings" subtitle="Create and revoke external API access." />
      <Card className="space-y-4 p-4">
        <div className="flex items-center gap-2">
          <KeyRound className="size-4 text-primary" />
          <h2 className="text-sm font-semibold">New API client</h2>
        </div>
        <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
          <input
            className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
            onChange={x => setName(x.target.value)}
            placeholder="Client name"
            value={name}
          />
          <Button disabled={createApiClient.isPending || !name.trim()} onClick={() => createApiClient.mutate()}>
            <Plus data-icon="inline-start" />
            Create key
          </Button>
        </div>
        {newApiKey && (
          <div className="rounded-md border border-border bg-muted p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm font-medium">Copy this key now. It will not be shown again.</p>
              <Button onClick={copyKey} size="sm" variant="outline">
                {copied ? <Check data-icon="inline-start" /> : <Copy data-icon="inline-start" />}
                {copied ? 'Copied' : 'Copy'}
              </Button>
            </div>
            <code className="mt-3 block overflow-x-auto rounded-md bg-background p-3 text-xs">{newApiKey}</code>
          </div>
        )}
      </Card>
      <div className="grid gap-3">
        {(apiClients.data ?? []).map(x => (
          <Card className="p-4" key={x.id}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="font-medium">{x.name}</p>
                <p className="text-sm text-muted-foreground">Created {new Date(x.createdAt).toLocaleString()}</p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={x.revokedAt ? 'secondary' : 'default'}>{x.revokedAt ? 'Revoked' : 'Active'}</Badge>
                {!x.revokedAt && (
                  <Button disabled={revokeApiClient.isPending} onClick={() => revoke(x)} size="sm" variant="destructive">
                    <ShieldOff data-icon="inline-start" />
                    Revoke
                  </Button>
                )}
              </div>
            </div>
            {x.revokedAt && <p className="mt-3 text-sm text-muted-foreground">Revoked {new Date(x.revokedAt).toLocaleString()}</p>}
          </Card>
        ))}
        {!apiClients.isLoading && apiClients.data?.length === 0 && <p className="text-sm text-muted-foreground">No API clients yet.</p>}
      </div>
    </section>
  )
}
