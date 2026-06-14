import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Edit3, Plus, RotateCcw, ShoppingCart, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Header } from '../components/Header'
import { CardGridLoading } from '../components/LoadingSkeletons'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { SpendingPlanner as SpendingPlannerModel, SpendingPlannerItem } from '../lib/types'

type FormState = {
  id?: string
  name: string
  amount: string
  targetDate: string
}

const emptyForm: FormState = {
  name: '',
  amount: '',
  targetDate: ''
}

export function SpendingPlanner() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<FormState>(emptyForm)
  const planner = useQuery({
    queryKey: ['spending-planner'],
    queryFn: () => api<SpendingPlannerModel>('/api/spending-planner'),
    placeholderData: keepPreviousData
  })
  const isEditing = !!form.id
  const activeItems = useMemo(() => (planner.data?.items ?? []).filter(x => !x.isPurchased), [planner.data?.items])
  const purchasedItems = useMemo(() => (planner.data?.items ?? []).filter(x => x.isPurchased), [planner.data?.items])
  const saveItem = useMutation({
    mutationFn: () => api<SpendingPlannerItem>(form.id ? `/api/spending-planner/items/${form.id}` : '/api/spending-planner/items', {
      method: form.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: form.name,
        amountMinorUnits: dollarsToMinorUnits(form.amount),
        currency: planner.data?.currency ?? 'AUD',
        targetDate: form.targetDate || null,
        isPurchased: planner.data?.items.find(x => x.id === form.id)?.isPurchased ?? false
      })
    }),
    onSuccess: () => {
      setForm(emptyForm)
      queryClient.invalidateQueries({ queryKey: ['spending-planner'] })
    }
  })
  const updateItem = useMutation({
    mutationFn: (item: SpendingPlannerItem) => api<SpendingPlannerItem>(`/api/spending-planner/items/${item.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: item.name,
        amountMinorUnits: item.amountMinorUnits,
        currency: item.currency,
        targetDate: item.targetDate ?? null,
        isPurchased: !item.isPurchased
      })
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['spending-planner'] })
    }
  })
  const deleteItem = useMutation({
    mutationFn: (itemId: string) => api<void>(`/api/spending-planner/items/${itemId}`, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['spending-planner'] })
    }
  })

  function editItem(item: SpendingPlannerItem) {
    setForm({
      id: item.id,
      name: item.name,
      amount: minorUnitsToDollars(item.amountMinorUnits),
      targetDate: item.targetDate ?? ''
    })
  }

  function removeItem(item: SpendingPlannerItem) {
    if (window.confirm(`Delete ${item.name}?`)) {
      deleteItem.mutate(item.id)
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Spending planner" subtitle="Plan upcoming purchases, compare them against savings, and mark items off once bought." />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <PlannerMetric label="Planned" value={currency(planner.data?.plannedTotalMinorUnits, planner.data?.currency ?? 'AUD')} />
        <PlannerMetric label="Savings" value={currency(planner.data?.savingsBalanceMinorUnits, planner.data?.currency ?? 'AUD')} />
        <PlannerMetric label="After planned spend" tone={(planner.data?.remainingSavingsMinorUnits ?? 0) < 0 ? 'danger' : 'default'} value={currency(planner.data?.remainingSavingsMinorUnits, planner.data?.currency ?? 'AUD')} />
        <PlannerMetric label="Purchased" value={currency(planner.data?.purchasedTotalMinorUnits, planner.data?.currency ?? 'AUD')} />
      </div>
      <Card>
        <CardHeader>
          <CardTitle>{isEditing ? 'Edit planned item' : 'Add planned item'}</CardTitle>
          <CardDescription>Purchased items stay in the list but no longer count toward the planned total.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,0.7fr)_minmax(0,0.8fr)_auto]">
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setForm(y => ({ ...y, name: x.target.value }))} placeholder="New TV, fridge, holiday deposit" value={form.name} />
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" inputMode="decimal" onChange={x => setForm(y => ({ ...y, amount: x.target.value }))} placeholder="Amount" value={form.amount} />
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setForm(y => ({ ...y, targetDate: x.target.value }))} type="date" value={form.targetDate} />
            <div className="flex gap-2">
              <Button disabled={saveItem.isPending || !form.name.trim() || !form.amount} onClick={() => saveItem.mutate()}>
                <Plus data-icon="inline-start" />
                {isEditing ? 'Save' : 'Add'}
              </Button>
              {isEditing && <Button onClick={() => setForm(emptyForm)} variant="outline">Cancel</Button>}
            </div>
          </div>
          {saveItem.error && <p className="text-sm text-destructive">Could not save this planned item.</p>}
        </CardContent>
      </Card>
      {planner.isLoading ? (
        <CardGridLoading />
      ) : (
        <div className={planner.isFetching ? 'grid gap-4 opacity-70 transition-opacity xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.45fr)]' : 'grid gap-4 transition-opacity xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.45fr)]'}>
          <PlannerList items={activeItems} onDelete={removeItem} onEdit={editItem} onTogglePurchased={x => updateItem.mutate(x)} title="Planned purchases" />
          <PlannerList isPurchasedList items={purchasedItems} onDelete={removeItem} onEdit={editItem} onTogglePurchased={x => updateItem.mutate(x)} title="Purchased" />
        </div>
      )}
      {!planner.isLoading && planner.data?.items.length === 0 && (
        <Card className="p-8 text-center">
          <ShoppingCart className="mx-auto size-8 text-muted-foreground" />
          <p className="mt-3 font-medium">No planned expenses yet</p>
          <p className="mt-1 text-sm text-muted-foreground">Add the bigger purchases or known upcoming expenses you want to keep visible.</p>
        </Card>
      )}
    </section>
  )
}

function PlannerMetric({ label, value, tone = 'default' }: { label: string; value: string; tone?: 'default' | 'danger' }) {
  return (
    <Card>
      <CardHeader>
        <p className="text-sm text-muted-foreground">{label}</p>
      </CardHeader>
      <CardContent>
        <p className={tone === 'danger' ? 'text-2xl font-semibold text-destructive' : 'text-2xl font-semibold'}>{value}</p>
      </CardContent>
    </Card>
  )
}

function PlannerList({ isPurchasedList = false, items, onDelete, onEdit, onTogglePurchased, title }: { isPurchasedList?: boolean; items: SpendingPlannerItem[]; onDelete: (item: SpendingPlannerItem) => void; onEdit: (item: SpendingPlannerItem) => void; onTogglePurchased: (item: SpendingPlannerItem) => void; title: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{items.length} {items.length === 1 ? 'item' : 'items'}</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="overflow-hidden rounded-md border border-border">
          {items.map(x => (
            <div className={x.isPurchased ? 'grid gap-3 border-b border-border bg-muted/50 px-3 py-3 last:border-b-0 sm:grid-cols-[auto_1fr_auto] sm:items-center' : 'grid gap-3 border-b border-border px-3 py-3 last:border-b-0 sm:grid-cols-[auto_1fr_auto] sm:items-center'} key={x.id}>
              <Button onClick={() => onTogglePurchased(x)} size="icon" title={x.isPurchased ? 'Mark as planned' : 'Mark as purchased'} variant={x.isPurchased ? 'secondary' : 'outline'}>
                {x.isPurchased ? <RotateCcw className="size-4" /> : <Check className="size-4" />}
              </Button>
              <div className="min-w-0">
                <p className={x.isPurchased ? 'truncate text-sm font-medium text-muted-foreground line-through' : 'truncate text-sm font-medium'}>{x.name}</p>
                <p className="truncate text-xs text-muted-foreground">{x.targetDate ? `Target ${formatDate(x.targetDate)}` : isPurchasedList ? 'Purchased' : 'No target date'}</p>
              </div>
              <div className="flex items-center justify-between gap-3 sm:justify-end">
                <p className="shrink-0 text-sm font-semibold">{currency(x.amountMinorUnits, x.currency)}</p>
                <div className="flex gap-2">
                  <Button onClick={() => onEdit(x)} size="icon" title="Edit planned item" variant="outline"><Edit3 className="size-4" /></Button>
                  <Button onClick={() => onDelete(x)} size="icon" title="Delete planned item" variant="destructive"><Trash2 className="size-4" /></Button>
                </div>
              </div>
            </div>
          ))}
          {items.length === 0 && <p className="px-3 py-3 text-sm text-muted-foreground">{isPurchasedList ? 'Nothing purchased yet.' : 'No active planned purchases.'}</p>}
        </div>
      </CardContent>
    </Card>
  )
}

function dollarsToMinorUnits(value: string) {
  return Math.round(Number.parseFloat(value || '0') * 100)
}

function minorUnitsToDollars(value: number) {
  return (value / 100).toFixed(2)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}
