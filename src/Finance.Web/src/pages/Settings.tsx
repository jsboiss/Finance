import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, Check, Copy, KeyRound, Plus, ShieldOff } from 'lucide-react'
import { useState } from 'react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ApiClient, CreateApiClientResponse, Tenant } from '../lib/types'

export function Settings() {
  const queryClient = useQueryClient()
  const [tenantName, setTenantName] = useState('')
  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [clientName, setClientName] = useState('External UI')
  const [newApiKey, setNewApiKey] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const tenants = useQuery({ queryKey: ['tenants'], queryFn: () => api<Tenant[]>('/api/tenants') })
  const apiClients = useQuery({ queryKey: ['api-clients'], queryFn: () => api<ApiClient[]>('/api/api-clients') })
  const createTenant = useMutation({
    mutationFn: () =>
      api<Tenant>('/api/tenants', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: tenantName })
      }),
    onSuccess: x => {
      setTenantName('')
      setSelectedTenantId(x.id)
      void queryClient.invalidateQueries({ queryKey: ['tenants'] })
    }
  })
  const createApiClient = useMutation({
    mutationFn: () =>
      api<CreateApiClientResponse>('/api/api-clients', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tenantId: selectedTenantId, name: clientName })
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
          <h2 className="text-sm font-semibold">New external API key</h2>
        </div>
        <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
          <select
            className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
            onChange={x => setSelectedTenantId(x.target.value)}
            value={selectedTenantId}
          >
            <option value="">Select tenant</option>
            {(tenants.data ?? []).map(x => (
              <option key={x.id} value={x.id}>
                {x.name}
              </option>
            ))}
          </select>
          <input
            className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
            onChange={x => setClientName(x.target.value)}
            placeholder="Client name"
            value={clientName}
          />
          <Button disabled={createApiClient.isPending || !selectedTenantId || !clientName.trim()} onClick={() => createApiClient.mutate()}>
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
        {createApiClient.error && <p className="text-sm text-destructive">Could not create API key. Check that the API is running with the latest build.</p>}
      </Card>
      <Card className="space-y-4 p-4">
        <div className="flex items-center gap-2">
          <Building2 className="size-4 text-primary" />
          <h2 className="text-sm font-semibold">Tenants</h2>
        </div>
        <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
          <input
            className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
            onChange={x => setTenantName(x.target.value)}
            placeholder="Tenant name"
            value={tenantName}
          />
          <Button disabled={createTenant.isPending || !tenantName.trim()} onClick={() => createTenant.mutate()} variant="outline">
            <Plus data-icon="inline-start" />
            {createTenant.isPending ? 'Creating' : 'Create tenant'}
          </Button>
        </div>
        {createTenant.error && <p className="text-sm text-destructive">Could not create tenant. Check that the API is running with the latest build and that the tenant name is unique.</p>}
        <div className="grid gap-2">
          {(tenants.data ?? []).map(x => (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-3 py-2" key={x.id}>
              <div>
                <p className="text-sm font-medium">{x.name}</p>
                <p className="text-xs text-muted-foreground">{x.id}</p>
              </div>
              <p className="text-xs text-muted-foreground">Created {new Date(x.createdAt).toLocaleString()}</p>
            </div>
          ))}
          {!tenants.isLoading && tenants.data?.length === 0 && <p className="text-sm text-muted-foreground">No tenants yet.</p>}
        </div>
      </Card>
      <div className="grid gap-3">
        {(apiClients.data ?? []).map(x => (
          <Card className="p-4" key={x.id}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="font-medium">{x.name}</p>
                <p className="text-sm text-muted-foreground">
                  {x.tenantName} - Created {new Date(x.createdAt).toLocaleString()}
                </p>
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
