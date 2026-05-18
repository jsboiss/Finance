import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Activity, Building2, Check, Copy, DatabaseZap, KeyRound, Plus, RefreshCcw, ShieldOff, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { api } from '../lib/api'
import type { ApiClient, CreateApiClientResponse, ImportRun, Tenant, TenantAdminAccount, TenantConnections } from '../lib/types'

export function Settings() {
  const queryClient = useQueryClient()
  const [tenantName, setTenantName] = useState('')
  const [selectedTenantId, setSelectedTenantId] = useState('')
  const [clientName, setClientName] = useState('External UI')
  const [selectedConnectionId, setSelectedConnectionId] = useState('')
  const [newApiKey, setNewApiKey] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const tenants = useQuery({ queryKey: ['tenants'], queryFn: () => api<Tenant[]>('/api/tenants') })
  const apiClients = useQuery({ queryKey: ['api-clients'], queryFn: () => api<ApiClient[]>('/api/api-clients') })
  const tenantConnections = useQuery({
    enabled: !!selectedTenantId,
    queryKey: ['tenant-connections', selectedTenantId],
    queryFn: () => api<TenantConnections>(`/api/admin/tenants/${selectedTenantId}/redbark/connections`)
  })
  const tenantAccounts = useQuery({
    enabled: !!selectedTenantId,
    queryKey: ['tenant-admin-accounts', selectedTenantId],
    queryFn: () => api<TenantAdminAccount[]>(`/api/admin/tenants/${selectedTenantId}/redbark/accounts`)
  })
  const tenantImports = useQuery({
    enabled: !!selectedTenantId,
    queryKey: ['tenant-imports', selectedTenantId],
    queryFn: () => api<ImportRun[]>(`/api/admin/tenants/${selectedTenantId}/imports`),
    refetchInterval: x => x.state.data?.some(y => y.status === 'running') ? 3000 : false
  })
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
  const assignConnection = useMutation({
    mutationFn: () =>
      api<void>(`/api/admin/tenants/${selectedTenantId}/redbark/connections`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ externalConnectionId: selectedConnectionId })
      }),
    onSuccess: () => {
      setSelectedConnectionId('')
      invalidateTenantAdminData(queryClient, selectedTenantId)
    }
  })
  const discoverAccounts = useTenantOperation(selectedTenantId, 'operations/accounts/discover', queryClient)
  const backfill = useTenantOperation(selectedTenantId, 'operations/backfill', queryClient)
  const reconcile = useTenantOperation(selectedTenantId, 'operations/reconcile', queryClient)
  const clearTenantData = useMutation({
    mutationFn: () => api<void>(`/api/admin/tenants/${selectedTenantId}/operations/data`, { method: 'DELETE' }),
    onSuccess: () => invalidateTenantAdminData(queryClient, selectedTenantId)
  })

  const isTenantOperationRunning = assignConnection.isPending || discoverAccounts.isPending || backfill.isPending || reconcile.isPending || clearTenantData.isPending

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

  function clearSelectedTenantData() {
    if (window.confirm('Clear imported banking data for this tenant? API clients and Redbark assignments will remain.')) {
      clearTenantData.mutate()
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
      <Card className="space-y-4 p-4">
        <div className="flex items-center gap-2">
          <DatabaseZap className="size-4 text-primary" />
          <h2 className="text-sm font-semibold">Tenant Redbark setup</h2>
        </div>
        {!selectedTenantId && <p className="text-sm text-muted-foreground">Select a tenant above to assign Redbark connections and run tenant-scoped imports.</p>}
        {selectedTenantId && (
          <>
            <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
              <select
                className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                onChange={x => setSelectedConnectionId(x.target.value)}
                value={selectedConnectionId}
              >
                <option value="">Select Redbark connection</option>
                {(tenantConnections.data?.available ?? []).map(x => (
                  <option disabled={!!x.assignedTenantName && !x.isAssignedToTenant} key={x.externalConnectionId} value={x.externalConnectionId}>
                    {x.institutionName || x.externalConnectionId}
                    {x.assignedTenantName ? ` - assigned to ${x.assignedTenantName}` : ''}
                  </option>
                ))}
              </select>
              <Button disabled={assignConnection.isPending || !selectedConnectionId} onClick={() => assignConnection.mutate()} variant="outline">
                <Plus data-icon="inline-start" />
                Assign connection
              </Button>
            </div>
            {assignConnection.error && <p className="text-sm text-destructive">Could not assign that Redbark connection. It may already be assigned to another tenant.</p>}
            <div className="grid gap-2">
              {(tenantConnections.data?.assigned ?? []).map(x => (
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-3 py-2" key={x.externalConnectionId}>
                  <div>
                    <p className="text-sm font-medium">{x.institutionName || x.externalConnectionId}</p>
                    <p className="text-xs text-muted-foreground">{x.externalConnectionId}</p>
                  </div>
                  <p className="text-xs text-muted-foreground">Assigned {new Date(x.createdAt).toLocaleString()}</p>
                </div>
              ))}
              {!tenantConnections.isLoading && tenantConnections.data?.assigned.length === 0 && <p className="text-sm text-muted-foreground">No Redbark connections assigned to this tenant.</p>}
            </div>
            {(tenantImports.data?.some(x => x.status === 'running') || isTenantOperationRunning) && (
              <div className="flex items-center gap-2 rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground">
                <Activity className="size-4 animate-pulse text-foreground" />
                <span>Tenant sync is running. Status refreshes automatically.</span>
              </div>
            )}
            <div className="flex flex-wrap gap-2">
              <Button disabled={isTenantOperationRunning} onClick={() => discoverAccounts.mutate()}>
                <DatabaseZap data-icon="inline-start" />
                {discoverAccounts.isPending ? 'Discovering' : 'Discover accounts'}
              </Button>
              <Button disabled={isTenantOperationRunning} onClick={() => reconcile.mutate()} variant="secondary">
                <RefreshCcw data-icon="inline-start" />
                {reconcile.isPending ? 'Reconciling' : 'Recent recon'}
              </Button>
              <Button disabled={isTenantOperationRunning} onClick={() => backfill.mutate()} variant="outline">
                <DatabaseZap data-icon="inline-start" />
                {backfill.isPending ? 'Syncing' : 'Backfill tenant'}
              </Button>
              <Button disabled={isTenantOperationRunning} onClick={clearSelectedTenantData} variant="destructive">
                <Trash2 data-icon="inline-start" />
                Clear imported data
              </Button>
            </div>
            {(discoverAccounts.error || backfill.error || reconcile.error || clearTenantData.error) && (
              <p className="text-sm text-destructive">Tenant operation failed. Check the latest import row for details.</p>
            )}
            <div className="grid gap-3 xl:grid-cols-2">
              <div className="grid gap-2">
                <p className="text-xs font-semibold uppercase text-muted-foreground">Imported account metadata</p>
                {(tenantAccounts.data ?? []).map(x => (
                  <div className="rounded-md border border-border px-3 py-2" key={x.id}>
                    <p className="text-sm font-medium">{x.customName || x.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {x.institutionName} - {x.accountNumber || 'No account number'} - {x.currency}
                    </p>
                  </div>
                ))}
                {!tenantAccounts.isLoading && tenantAccounts.data?.length === 0 && <p className="text-sm text-muted-foreground">No account metadata imported yet.</p>}
              </div>
              <div className="grid gap-2">
                <p className="text-xs font-semibold uppercase text-muted-foreground">Latest tenant imports</p>
                {(tenantImports.data ?? []).map(x => (
                  <div className="rounded-md border border-border px-3 py-2" key={x.id}>
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-medium">{x.source}</p>
                      <Badge variant={x.status === 'failed' ? 'destructive' : 'secondary'}>{x.status}</Badge>
                    </div>
                    <p className="text-xs text-muted-foreground">{x.importedCount} imported - {new Date(x.startedAt).toLocaleString()}</p>
                    {x.error && <p className="mt-1 text-xs text-destructive">{x.error}</p>}
                  </div>
                ))}
                {!tenantImports.isLoading && tenantImports.data?.length === 0 && <p className="text-sm text-muted-foreground">No tenant imports yet.</p>}
              </div>
            </div>
          </>
        )}
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

function useTenantOperation(selectedTenantId: string, path: string, queryClient: ReturnType<typeof useQueryClient>) {
  return useMutation({
    mutationFn: () => api<void>(`/api/admin/tenants/${selectedTenantId}/${path}`, { method: 'POST' }),
    onSuccess: () => invalidateTenantAdminData(queryClient, selectedTenantId)
  })
}

function invalidateTenantAdminData(queryClient: ReturnType<typeof useQueryClient>, tenantId: string) {
  void queryClient.invalidateQueries({ queryKey: ['tenant-connections', tenantId] })
  void queryClient.invalidateQueries({ queryKey: ['tenant-admin-accounts', tenantId] })
  void queryClient.invalidateQueries({ queryKey: ['tenant-imports', tenantId] })
}
