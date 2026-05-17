import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronDown, ChevronRight, Plus, RefreshCw, Trash2, X } from 'lucide-react'
import { useState } from 'react'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Subscription, SubscriptionDetail, SubscriptionSuggestion } from '../lib/types'

const cadences = ['weekly', 'fortnightly', 'monthly', 'yearly']
const paymentManagers = ['direct', 'apple', 'paypal']
const statuses = ['', 'active', 'needsReview', 'inactive']

type SubscriptionForm = {
  id?: string
  name: string
  merchantName: string
  paymentManager: string
  cadence: string
  expectedAmount: string
  currency: string
  statusOverride: string
  isCancelled: boolean
}

const emptyForm: SubscriptionForm = {
  name: '',
  merchantName: '',
  paymentManager: 'direct',
  cadence: 'monthly',
  expectedAmount: '',
  currency: 'AUD',
  statusOverride: '',
  isCancelled: false
}

export function Subscriptions() {
  const [form, setForm] = useState<SubscriptionForm>(emptyForm)
  const [selectedSubscriptionId, setSelectedSubscriptionId] = useState<string | null>(null)
  const [editingSubscriptionId, setEditingSubscriptionId] = useState<string | null>(null)
  const [showSuggestions, setShowSuggestions] = useState(false)
  const queryClient = useQueryClient()
  const subscriptions = useQuery({ queryKey: ['subscriptions'], queryFn: () => api<Subscription[]>('/api/subscriptions') })
  const suggestions = useQuery({ queryKey: ['subscription-suggestions'], queryFn: () => api<SubscriptionSuggestion[]>('/api/subscription-suggestions') })
  const detail = useQuery({
    enabled: selectedSubscriptionId != null,
    queryKey: ['subscription', selectedSubscriptionId],
    queryFn: () => api<SubscriptionDetail>(`/api/subscriptions/${selectedSubscriptionId}`)
  })
  const refreshSuggestions = useMutation({
    mutationFn: () => api<SubscriptionSuggestion[]>('/api/subscription-suggestions/refresh', { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['subscription-suggestions'] })
  })
  const acceptSuggestion = useMutation({
    mutationFn: (suggestionId: string) => api<Subscription>(`/api/subscription-suggestions/${suggestionId}/accept`, { method: 'POST' }),
    onSuccess: x => {
      setSelectedSubscriptionId(x.id)
      queryClient.invalidateQueries({ queryKey: ['subscriptions'] })
      queryClient.invalidateQueries({ queryKey: ['subscription-suggestions'] })
    }
  })
  const dismissSuggestion = useMutation({
    mutationFn: (suggestionId: string) => api<void>(`/api/subscription-suggestions/${suggestionId}/dismiss`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['subscription-suggestions'] })
  })
  const saveSubscription = useMutation({
    mutationFn: () => api<Subscription>(form.id ? `/api/subscriptions/${form.id}` : '/api/subscriptions', {
      method: form.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: form.name,
        merchantName: form.merchantName,
        paymentManager: form.paymentManager,
        cadence: form.cadence,
        expectedAmountMinorUnits: Math.round(Number(form.expectedAmount || '0') * 100),
        currency: form.currency,
        statusOverride: form.statusOverride || null,
        isCancelled: form.isCancelled
      })
    }),
    onSuccess: x => {
      setSelectedSubscriptionId(x.id)
      setForm(emptyForm)
      setEditingSubscriptionId(null)
      queryClient.invalidateQueries({ queryKey: ['subscriptions'] })
      queryClient.invalidateQueries({ queryKey: ['subscription', x.id] })
    }
  })
  const deleteSubscription = useMutation({
    mutationFn: (subscriptionId: string) => api<void>(`/api/subscriptions/${subscriptionId}`, { method: 'DELETE' }),
    onSuccess: () => {
      setSelectedSubscriptionId(null)
      queryClient.invalidateQueries({ queryKey: ['subscriptions'] })
    }
  })
  const activeSubscriptions = (subscriptions.data ?? []).filter(x => getDisplayStatus(x).activity === 'active')
  const sortedSuggestions = [...(suggestions.data ?? [])].sort((x, y) => y.lastPaymentDate.localeCompare(x.lastPaymentDate))
  const monthlyEstimate = activeSubscriptions.reduce((x, y) => x + y.monthlyEstimateMinorUnits, 0)
  const yearlyEstimate = activeSubscriptions.reduce((x, y) => x + y.yearlyEstimateMinorUnits, 0)

  return (
    <section className="space-y-6">
      <Header title="Subscriptions" subtitle="Track recurring payments, review detected subscriptions, and watch for price increases." />
      <div className="grid gap-3 md:grid-cols-4">
        <Metric label="Active" value={`${activeSubscriptions.length}`} />
        <Metric label="Monthly estimate" value={currency(monthlyEstimate, 'AUD')} />
        <Metric label="Yearly estimate" value={currency(yearlyEstimate, 'AUD')} />
        <Metric label="Pending suggestions" value={`${suggestions.data?.length ?? 0}`} />
      </div>
      <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
        <div className="space-y-6">
          <div className="overflow-hidden rounded-lg border border-border bg-card">
            <Table>
              <TableHeader className="bg-muted text-xs uppercase text-muted-foreground">
                <TableRow>
                  <TableHead className="px-4 py-3">Subscription</TableHead>
                  <TableHead className="px-4 py-3">Cadence</TableHead>
                  <TableHead className="px-4 py-3">Expected</TableHead>
                  <TableHead className="px-4 py-3">Last paid</TableHead>
                  <TableHead className="px-4 py-3">Next due</TableHead>
                  <TableHead className="px-4 py-3">Status</TableHead>
                  <TableHead className="px-4 py-3">Total paid</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y divide-border">
                {(subscriptions.data ?? []).map(x => (
                  <TableRow className="cursor-pointer hover:bg-muted/50" key={x.id} onClick={() => openEditor(x)}>
                    <TableCell className="px-4 py-3">
                      <p className="font-medium">{x.name}</p>
                      <p className="text-xs text-muted-foreground">{x.merchantName}</p>
                    </TableCell>
                    <TableCell className="px-4 py-3">{x.cadence}</TableCell>
                    <TableCell className="px-4 py-3">{currency(x.expectedAmountMinorUnits, x.currency)}</TableCell>
                    <TableCell className="px-4 py-3">{formatDate(x.lastPaymentDate)}</TableCell>
                    <TableCell className="px-4 py-3">{formatDate(x.nextExpectedPaymentDate)}</TableCell>
                    <TableCell className="px-4 py-3"><StatusBadge subscription={x} /></TableCell>
                    <TableCell className="px-4 py-3">{currency(x.totalPaidMinorUnits, x.currency)}</TableCell>
                  </TableRow>
                ))}
                {!subscriptions.isLoading && (subscriptions.data?.length ?? 0) === 0 && <TableRow><TableCell className="px-4 py-8 text-muted-foreground" colSpan={7}>No subscriptions tracked yet.</TableCell></TableRow>}
              </TableBody>
            </Table>
          </div>
          <Card className="space-y-4 p-4">
            <div className="flex items-center justify-between gap-3">
              <button className="inline-flex items-center gap-2 text-sm font-semibold" onClick={() => setShowSuggestions(x => !x)} type="button">
                {showSuggestions ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                Suggested subscriptions ({suggestions.data?.length ?? 0})
              </button>
              <Button disabled={refreshSuggestions.isPending} onClick={() => refreshSuggestions.mutate()} size="sm" variant="outline">
                <RefreshCw data-icon="inline-start" />
                Refresh
              </Button>
            </div>
            {showSuggestions && (
              <div className="grid gap-2">
                {sortedSuggestions.map(x => (
                  <div className="grid gap-3 rounded-md border border-border p-3 md:grid-cols-[1fr_auto]" key={x.id}>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium">{x.merchantName}</p>
                        <Badge>{x.paymentManager}</Badge>
                        <Badge variant="secondary">{x.confidence}% confidence</Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">
                        {currency(x.expectedAmountMinorUnits, x.currency)} {x.cadence}, last paid {formatDate(x.lastPaymentDate)}, next expected {formatDate(x.nextExpectedPaymentDate)}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button disabled={acceptSuggestion.isPending} onClick={() => acceptSuggestion.mutate(x.id)} size="sm">
                        <Check data-icon="inline-start" />
                        Accept
                      </Button>
                      <Button disabled={dismissSuggestion.isPending} onClick={() => dismissSuggestion.mutate(x.id)} size="sm" variant="outline">
                        <X data-icon="inline-start" />
                        Dismiss
                      </Button>
                    </div>
                  </div>
                ))}
                {!suggestions.isLoading && sortedSuggestions.length === 0 && <p className="text-sm text-muted-foreground">No pending suggestions.</p>}
              </div>
            )}
          </Card>
        </div>
        <aside className="space-y-6">
          <Card className="space-y-3 p-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-semibold">Manual subscription</h2>
            </div>
            <SubscriptionFormFields form={form} onChange={setForm} />
            <Button disabled={!form.name.trim() || !form.merchantName.trim() || saveSubscription.isPending} onClick={() => saveSubscription.mutate()} size="sm">
              <Plus data-icon="inline-start" />
              Add
            </Button>
          </Card>
        </aside>
      </div>
      {editingSubscriptionId && (
        <div className="fixed inset-0 z-30 grid place-items-center bg-black/35 p-4" onClick={closeEditor}>
          <div className="flex max-h-[90vh] w-full max-w-4xl flex-col overflow-hidden rounded-lg border border-border bg-card shadow-xl" onClick={x => x.stopPropagation()}>
            <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-4">
              <div>
                <h2 className="text-sm font-semibold">Edit subscription</h2>
                {detail.data && <p className="text-xs text-muted-foreground">Next expected {formatDate(detail.data.subscription.nextExpectedPaymentDate)}</p>}
              </div>
              <button aria-label="Close editor" className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground" onClick={closeEditor} type="button">
                <X className="size-4" />
              </button>
            </div>
            <div className="grid min-h-0 min-w-0 flex-1 gap-0 lg:grid-cols-[minmax(0,1fr)_340px]">
              <div className="min-h-0 min-w-0 space-y-5 overflow-y-auto p-5">
                {detail.data ? (
                  <>
                    <section className="min-w-0 rounded-md border border-border bg-background p-3">
                      <h3 className="text-xs font-semibold uppercase text-muted-foreground">Price changes</h3>
                      <div className="mt-2 space-y-2">
                        {detail.data.subscription.priceChanges.map(x => (
                        <p className="break-words text-sm" key={`${x.effectiveDate}-${x.newAmountMinorUnits}`}>
                          {formatDate(x.effectiveDate)}: {currency(x.previousAmountMinorUnits, detail.data.subscription.currency)} to {currency(x.newAmountMinorUnits, detail.data.subscription.currency)} ({x.status})
                        </p>
                        ))}
                        {detail.data.subscription.priceChanges.length === 0 && <p className="text-sm text-muted-foreground">No price increases detected.</p>}
                      </div>
                    </section>
                    <section className="min-w-0 rounded-md border border-border bg-background p-3">
                      <h3 className="text-xs font-semibold uppercase text-muted-foreground">Payments</h3>
                      <div className="mt-2 space-y-2">
                        {detail.data.payments.slice(0, 12).map(x => (
                          <div className="grid min-w-0 gap-1 rounded-md bg-muted/50 p-2 text-sm sm:grid-cols-[minmax(0,1fr)_auto]" key={x.transactionId}>
                            <span className="min-w-0 break-words">{formatDate(x.postedDate)} · {x.description}</span>
                            <span className="whitespace-nowrap font-medium">{currency(x.amountMinorUnits, x.currency)}</span>
                          </div>
                        ))}
                        {detail.data.payments.length === 0 && <p className="text-sm text-muted-foreground">No linked payments yet.</p>}
                      </div>
                    </section>
                  </>
                ) : (
                  <p className="text-sm text-muted-foreground">Loading subscription history...</p>
                )}
              </div>
              <div className="min-w-0 space-y-3 overflow-y-visible border-t border-border p-5 lg:border-l lg:border-t-0">
                <h3 className="text-xs font-semibold uppercase text-muted-foreground">Details</h3>
                <SubscriptionFormFields form={form} onChange={setForm} />
                <div className="flex justify-between gap-2">
                  <Button disabled={deleteSubscription.isPending} onClick={() => deleteSubscription.mutate(editingSubscriptionId)} size="sm" variant="outline">
                    <Trash2 data-icon="inline-start" />
                    Delete
                  </Button>
                  <div className="flex gap-2">
                    <Button onClick={closeEditor} size="sm" variant="outline">Cancel</Button>
                    <Button disabled={!form.name.trim() || !form.merchantName.trim() || saveSubscription.isPending} onClick={() => saveSubscription.mutate()} size="sm">
                      Save
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </section>
  )

  function openEditor(subscription: Subscription) {
    setSelectedSubscriptionId(subscription.id)
    setEditingSubscriptionId(subscription.id)
    setForm(toForm(subscription))
  }

  function closeEditor() {
    setEditingSubscriptionId(null)
    setForm(emptyForm)
  }
}

function SubscriptionFormFields({ form, onChange }: { form: SubscriptionForm; onChange: (form: SubscriptionForm) => void }) {
  return (
    <div className="grid gap-2">
      <input className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, name: x.target.value })} placeholder="Name" value={form.name} />
      <input className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, merchantName: x.target.value })} placeholder="Merchant name" value={form.merchantName} />
      <div className="grid grid-cols-2 gap-2">
        <select className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, paymentManager: x.target.value })} value={form.paymentManager}>
          {paymentManagers.map(x => <option key={x} value={x}>{x}</option>)}
        </select>
        <select className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, cadence: x.target.value })} value={form.cadence}>
          {cadences.map(x => <option key={x} value={x}>{x}</option>)}
        </select>
      </div>
      <div className="grid grid-cols-[1fr_88px] gap-2">
        <input className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, expectedAmount: x.target.value })} placeholder="Expected amount" type="number" value={form.expectedAmount} />
        <input className="h-9 rounded-md border border-input bg-background px-2 text-sm uppercase outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, currency: x.target.value })} value={form.currency} />
      </div>
      <select className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => onChange({ ...form, statusOverride: x.target.value })} value={form.statusOverride}>
        {statuses.map(x => <option key={x || 'inferred'} value={x}>{x || 'Inferred status'}</option>)}
      </select>
      <label className="inline-flex items-center gap-2 rounded-md border border-input bg-background px-2 py-2 text-sm">
        <input checked={form.isCancelled} className="h-4 w-4" onChange={x => onChange({ ...form, isCancelled: x.target.checked })} type="checkbox" />
        Cancelled
      </label>
    </div>
  )
}

function toForm(subscription: Subscription): SubscriptionForm {
  return {
    id: subscription.id,
    name: subscription.name,
    merchantName: subscription.merchantName,
    paymentManager: subscription.paymentManager,
    cadence: subscription.cadence,
    expectedAmount: `${subscription.expectedAmountMinorUnits / 100}`,
    currency: subscription.currency,
    statusOverride: subscription.statusOverride ?? '',
    isCancelled: subscription.isCancelled
  }
}

function StatusBadge({ subscription }: { subscription: Subscription }) {
  const status = getDisplayStatus(subscription)
  return <Badge variant={status.activity === 'active' ? 'default' : 'secondary'}>{status.label}</Badge>
}

function getDisplayStatus(subscription: Subscription) {
  if (!subscription.isCancelled) {
    return { activity: subscription.status, label: subscription.status }
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const nextDue = subscription.nextExpectedPaymentDate ? new Date(subscription.nextExpectedPaymentDate) : null
  const isStillUsable = nextDue != null && today <= nextDue
  return {
    activity: isStillUsable ? 'active' : 'inactive',
    label: `${isStillUsable ? 'Active' : 'Inactive'} - cancelled`
  }
}

function formatDate(value?: string) {
  return value ? new Intl.DateTimeFormat('en-AU', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)) : 'Unknown'
}
