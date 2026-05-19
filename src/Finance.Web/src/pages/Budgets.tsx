import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Edit3, Plus, Target, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Header } from '../components/Header'
import { CardGridLoading } from '../components/LoadingSkeletons'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { BudgetProfile, BudgetWeek, TransactionTag } from '../lib/types'

type FormState = {
  id?: string
  name: string
  weeklyLimit: string
  weekStartsOn: string
  categoryMatchers: string
  tagIds: string[]
}

const emptyForm: FormState = {
  name: 'Eating out',
  weeklyLimit: '',
  weekStartsOn: '1',
  categoryMatchers: 'Eating Out, Restaurants, Fast Food, Cafes',
  tagIds: []
}

const weekDays = [
  { value: '0', label: 'Sunday' },
  { value: '1', label: 'Monday' },
  { value: '2', label: 'Tuesday' },
  { value: '3', label: 'Wednesday' },
  { value: '4', label: 'Thursday' },
  { value: '5', label: 'Friday' },
  { value: '6', label: 'Saturday' }
]

export function Budgets() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<FormState>(emptyForm)
  const [expandedProfileId, setExpandedProfileId] = useState<string | null>(null)
  const tags = useQuery({ queryKey: ['tags'], queryFn: () => api<TransactionTag[]>('/api/tags') })
  const budgets = useQuery({
    queryKey: ['budgets'],
    queryFn: () => api<BudgetProfile[]>('/api/budgets'),
    placeholderData: keepPreviousData
  })
  const isEditing = !!form.id
  const selectedTags = useMemo(() => (tags.data ?? []).filter(x => form.tagIds.includes(x.id)), [form.tagIds, tags.data])
  const saveBudget = useMutation({
    mutationFn: () => api<BudgetProfile>(form.id ? `/api/budgets/${form.id}` : '/api/budgets', {
      method: form.id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: form.name,
        weeklyLimitMinorUnits: dollarsToMinorUnits(form.weeklyLimit),
        currency: 'AUD',
        weekStartsOn: Number(form.weekStartsOn),
        categoryMatchers: splitCategoryMatchers(form.categoryMatchers),
        tagIds: form.tagIds
      })
    }),
    onSuccess: budget => {
      setForm(emptyForm)
      queryClient.setQueryData<BudgetProfile[]>(['budgets'], x => {
        const budgets = x ?? []
        return budgets.some(y => y.id === budget.id)
          ? budgets.map(y => y.id === budget.id ? budget : y)
          : [...budgets, budget]
      })
    }
  })
  const deleteBudget = useMutation({
    mutationFn: (budgetId: string) => api<void>(`/api/budgets/${budgetId}`, { method: 'DELETE' }),
    onMutate: async budgetId => {
      await queryClient.cancelQueries({ queryKey: ['budgets'] })
      const previousBudgets = queryClient.getQueryData<BudgetProfile[]>(['budgets'])
      queryClient.setQueryData<BudgetProfile[]>(['budgets'], x => (x ?? []).filter(y => y.id !== budgetId))
      return { previousBudgets }
    },
    onError: (_error, _budgetId, context) => {
      queryClient.setQueryData(['budgets'], context?.previousBudgets)
    }
  })

  function editBudget(budget: BudgetProfile) {
    setForm({
      id: budget.id,
      name: budget.name,
      weeklyLimit: minorUnitsToDollars(budget.weeklyLimitMinorUnits),
      weekStartsOn: `${budget.weekStartsOn}`,
      categoryMatchers: budget.categoryMatchers.join(', '),
      tagIds: budget.tags.map(x => x.id)
    })
  }

  function removeBudget(budget: BudgetProfile) {
    if (window.confirm(`Delete ${budget.name}?`)) {
      deleteBudget.mutate(budget.id)
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Budgets" subtitle="Track weekly eating-out spend using transaction categories plus selected tags." />
      <Card>
        <CardHeader>
          <CardTitle>{isEditing ? 'Edit budget' : 'Weekly eating-out budget'}</CardTitle>
          <CardDescription>Transactions count when their category matches one of the category terms, or when they have one of the selected tags.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,0.6fr)_minmax(0,0.7fr)_minmax(0,1.5fr)]">
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setForm(y => ({ ...y, name: x.target.value }))} placeholder="Budget name" value={form.name} />
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" inputMode="decimal" onChange={x => setForm(y => ({ ...y, weeklyLimit: x.target.value }))} placeholder="Weekly limit" value={form.weeklyLimit} />
            <select className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setForm(y => ({ ...y, weekStartsOn: x.target.value }))} value={form.weekStartsOn}>
              {weekDays.map(x => <option key={x.value} value={x.value}>{x.label}</option>)}
            </select>
            <input className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setForm(y => ({ ...y, categoryMatchers: x.target.value }))} placeholder="Category terms" value={form.categoryMatchers} />
          </div>
          <div className="flex flex-wrap gap-2">
            {(tags.data ?? []).map(x => (
              <label className="inline-flex cursor-pointer items-center gap-2 rounded-md border border-border bg-muted px-2 py-1 text-xs" key={x.id}>
                <input checked={form.tagIds.includes(x.id)} onChange={y => setForm(z => ({ ...z, tagIds: y.target.checked ? [...z.tagIds, x.id] : z.tagIds.filter(a => a !== x.id) }))} type="checkbox" />
                <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: x.color }} />
                {x.name}
              </label>
            ))}
            {!tags.isLoading && (tags.data?.length ?? 0) === 0 && <span className="text-sm text-muted-foreground">Create tags from Transactions to include tagged spend here.</span>}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button disabled={saveBudget.isPending || !form.name.trim() || !form.weeklyLimit} onClick={() => saveBudget.mutate()}>
              <Plus data-icon="inline-start" />
              {isEditing ? 'Save' : 'Add'}
            </Button>
            {isEditing && <Button onClick={() => setForm(emptyForm)} variant="outline">Cancel</Button>}
            {selectedTags.length > 0 && <span className="text-sm text-muted-foreground">Including {selectedTags.map(x => x.name).join(', ')}</span>}
          </div>
          {saveBudget.error && <p className="text-sm text-destructive">Could not save this budget. Check that the name is unique.</p>}
        </CardContent>
      </Card>
      {budgets.isLoading ? (
        <CardGridLoading />
      ) : (
        <div className={budgets.isFetching ? 'grid gap-4 opacity-70 transition-opacity xl:grid-cols-2' : 'grid gap-4 transition-opacity xl:grid-cols-2'}>
          {(budgets.data ?? []).map(x => (
            <BudgetCard
              budget={x}
              isHistoryOpen={expandedProfileId === x.id}
              key={x.id}
              onDelete={removeBudget}
              onEdit={editBudget}
              onToggleHistory={() => setExpandedProfileId(y => y === x.id ? null : x.id)}
            />
          ))}
        </div>
      )}
      {!budgets.isLoading && budgets.data?.length === 0 && (
        <Card className="p-8 text-center">
          <Target className="mx-auto size-8 text-muted-foreground" />
          <p className="mt-3 font-medium">No budgets yet</p>
          <p className="mt-1 text-sm text-muted-foreground">Set a weekly limit and choose the categories or tags that should count toward it.</p>
        </Card>
      )}
    </section>
  )
}

function BudgetCard({ budget, isHistoryOpen, onDelete, onEdit, onToggleHistory }: { budget: BudgetProfile; isHistoryOpen: boolean; onDelete: (budget: BudgetProfile) => void; onEdit: (budget: BudgetProfile) => void; onToggleHistory: () => void }) {
  const currentWeek = budget.currentWeek
  const usedPercent = Math.min(Math.max(currentWeek.usedPercent, 0), 100)

  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{budget.name}</CardTitle>
          <CardDescription>{formatDate(currentWeek.from)} to {formatDate(currentWeek.to)} - starts {getWeekDayLabel(budget.weekStartsOn)}</CardDescription>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => onEdit(budget)} size="icon" title="Edit budget" variant="outline"><Edit3 className="size-4" /></Button>
          <Button onClick={() => onDelete(budget)} size="icon" title="Delete budget" variant="destructive"><Trash2 className="size-4" /></Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-3">
          <BudgetMetric label="Limit" value={budget.weeklyLimitMinorUnits} currencyCode={budget.currency} />
          <BudgetMetric label="Spent" value={currentWeek.spentMinorUnits} currencyCode={budget.currency} />
          <BudgetMetric label="Remaining" value={currentWeek.remainingMinorUnits} currencyCode={budget.currency} />
        </div>
        <div className="space-y-2">
          <div className="h-3 overflow-hidden rounded-full bg-muted">
            <div className={currentWeek.remainingMinorUnits < 0 ? 'h-full rounded-full bg-destructive' : 'h-full rounded-full bg-primary'} style={{ width: `${usedPercent}%` }} />
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
            <span>{currentWeek.usedPercent}% used</span>
            <span>{currentWeek.transactions.length} transactions</span>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {budget.categoryMatchers.map(x => <Badge key={x} variant="secondary">{x}</Badge>)}
          {budget.tags.map(x => <Badge key={x.id} variant="outline">{x.name}</Badge>)}
        </div>
        <WeekTransactions week={currentWeek} />
        <Button onClick={onToggleHistory} size="sm" variant="outline">{isHistoryOpen ? 'Hide history' : 'Show history'}</Button>
        {isHistoryOpen && <BudgetHistory budget={budget} />}
      </CardContent>
    </Card>
  )
}

function BudgetMetric({ label, value, currencyCode }: { label: string; value: number; currencyCode: string }) {
  return (
    <div className="rounded-md border border-border bg-muted p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={value < 0 ? 'mt-1 text-lg font-semibold text-destructive' : 'mt-1 text-lg font-semibold'}>{currency(value, currencyCode)}</p>
    </div>
  )
}

function WeekTransactions({ week }: { week: BudgetWeek }) {
  return (
    <div className="overflow-hidden rounded-md border border-border">
      {week.transactions.slice(0, 6).map(x => (
        <div className="grid gap-1 border-b border-border px-3 py-2 last:border-b-0 sm:grid-cols-[1fr_auto] sm:items-center" key={x.id}>
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{x.merchantName || x.description}</p>
            <p className="truncate text-xs text-muted-foreground">{formatDate(x.postedDate)} - {x.category}</p>
          </div>
          <p className="text-sm font-semibold sm:text-right">{currency(x.amountMinorUnits, x.currency)}</p>
        </div>
      ))}
      {week.transactions.length === 0 && <p className="px-3 py-2 text-sm text-muted-foreground">No matching spend this week.</p>}
    </div>
  )
}

function BudgetHistory({ budget }: { budget: BudgetProfile }) {
  return (
    <div className="grid gap-2">
      {budget.history.map(x => (
        <div className="grid gap-1 rounded-md border border-border p-3" key={x.from}>
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="font-medium">{formatDate(x.from)} to {formatDate(x.to)}</span>
            <span className={x.remainingMinorUnits < 0 ? 'font-semibold text-destructive' : 'font-semibold'}>{currency(x.spentMinorUnits, budget.currency)}</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div className={x.remainingMinorUnits < 0 ? 'h-full rounded-full bg-destructive' : 'h-full rounded-full bg-primary'} style={{ width: `${Math.min(Math.max(x.usedPercent, 0), 100)}%` }} />
          </div>
        </div>
      ))}
    </div>
  )
}

function splitCategoryMatchers(value: string) {
  return value.split(',').map(x => x.trim()).filter(Boolean)
}

function dollarsToMinorUnits(value: string) {
  return Math.round(Number.parseFloat(value || '0') * 100)
}

function minorUnitsToDollars(value: number) {
  return (value / 100).toFixed(2)
}

function getWeekDayLabel(value: number) {
  return weekDays.find(x => x.value === `${value}`)?.label ?? 'Monday'
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
}
