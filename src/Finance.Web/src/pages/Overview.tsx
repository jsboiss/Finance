import { useMemo, useState } from 'react'
import type React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CircleDollarSign, ReceiptText } from 'lucide-react'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Transaction, TransactionTag } from '../lib/types'

type MonthSpend = {
  key: string
  label: string
  total: number
  tags: Map<string, number>
}

type TagSpend = {
  id: string
  name: string
  color: string
  total: number
  current: number
  previous: number
  months: number[]
}

const fallbackTag: TransactionTag = { id: 'untagged', name: 'Untagged', color: '#94a3b8' }

export function Overview() {
  const [cashFlowAccountId, setCashFlowAccountId] = useState('all')
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const transactions = useQuery({ queryKey: ['transactions', 'overview'], queryFn: getOverviewTransactions })
  const availableBalances = accounts.data?.filter(x => x.currentBalanceMinorUnits != null) ?? []
  const totalBalance = availableBalances.length > 0 ? availableBalances.reduce((x, y) => x + (y.currentBalanceMinorUnits ?? 0), 0) : null
  const analysis = useMemo(() => analyzeSpending(transactions.data ?? []), [transactions.data])
  const cashFlow = useMemo(() => analyzeCashFlow(transactions.data ?? [], analysis.currentMonthKey, cashFlowAccountId), [transactions.data, analysis.currentMonthKey, cashFlowAccountId])

  return (
    <section className="space-y-6">
      <Header title="Overview" subtitle="Tagged spending patterns, trend changes, and unusually large transactions." />
      <div className="grid gap-4 md:grid-cols-4">
        <Metric label="Total balance" value={currency(totalBalance, 'AUD')} />
        <Metric label="This month spent" value={currency(analysis.currentMonthSpend, 'AUD')} />
        <Metric label="Month change" value={formatSignedCurrency(analysis.monthChange, 'AUD')} />
        <Metric label="Tagged coverage" value={`${analysis.taggedCoverage}%`} />
      </div>

      <Card>
        <CardHeader className="gap-3 sm:grid-cols-[1fr_auto] sm:items-start">
          <div>
            <CardTitle>Cash flow race</CardTitle>
            <CardDescription>{analysis.currentMonthLabel}</CardDescription>
          </div>
          <select
            aria-label="Cash flow account"
            className="h-9 rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30 sm:w-64"
            onChange={x => setCashFlowAccountId(x.target.value)}
            value={cashFlowAccountId}
          >
            <option value="all">All accounts</option>
            {(accounts.data ?? []).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
          </select>
        </CardHeader>
        <CardContent>
          <CashFlowRace income={cashFlow.income} expenses={cashFlow.expenses} />
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.4fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Monthly spending by tag</CardTitle>
            <CardDescription>{analysis.timeframeLabel}</CardDescription>
          </CardHeader>
          <CardContent>
            <StackedMonthlyBars months={analysis.months} tags={analysis.topTags} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Largest spending categories</CardTitle>
            <CardDescription>{analysis.timeframeLabel}</CardDescription>
          </CardHeader>
          <CardContent>
            <ParetoList tags={analysis.topTags} total={analysis.totalSpend} />
          </CardContent>
        </Card>
      </div>
    </section>
  )
}

function analyzeSpending(transactions: Transaction[]) {
  const posted = transactions.filter(x => x.status.toLowerCase() === 'posted')
  const expenses = posted.filter(x => x.amountMinorUnits < 0)
  const income = posted.filter(x => x.amountMinorUnits > 0)
  const monthKeys = [...new Set(expenses.map(x => x.postedDate.slice(0, 7)))].sort().slice(-6)
  const months: MonthSpend[] = monthKeys.map(x => ({
    key: x,
    label: formatMonthLabel(x),
    total: 0,
    tags: new Map<string, number>()
  }))
  const monthMap = new Map(months.map(x => [x.key, x]))
  const tagMap = new Map<string, TagSpend>()
  let taggedCount = 0

  for (const transaction of expenses) {
    const amount = Math.abs(transaction.amountMinorUnits)
    const tags = transaction.tags.length > 0 ? transaction.tags : [fallbackTag]
    const tagAmount = amount / tags.length
    const month = monthMap.get(transaction.postedDate.slice(0, 7))
    const monthIndex = monthKeys.indexOf(transaction.postedDate.slice(0, 7))
    if (transaction.tags.length > 0) {
      taggedCount += 1
    }

    if (month && monthIndex >= 0) {
      month.total += amount
    }

    for (const tag of tags) {
      if (!tagMap.has(tag.id)) {
        tagMap.set(tag.id, {
          id: tag.id,
          name: tag.name,
          color: tag.color,
          total: 0,
          current: 0,
          previous: 0,
          months: monthKeys.map(() => 0)
        })
      }

      const tagSpend = tagMap.get(tag.id)!
      tagSpend.total += tagAmount

      if (month && monthIndex >= 0) {
        month.tags.set(tag.id, (month.tags.get(tag.id) ?? 0) + tagAmount)
        tagSpend.months[monthIndex] += tagAmount
      }
    }
  }

  const topTags = [...tagMap.values()].sort((x, y) => y.total - x.total)
  for (const tag of topTags) {
    tag.current = tag.months.at(-1) ?? 0
    tag.previous = tag.months.at(-2) ?? 0
  }

  const currentMonthSpend = months.at(-1)?.total ?? 0
  const previousMonthSpend = months.at(-2)?.total ?? 0
  const currentMonthKey = months.at(-1)?.key ?? new Date().toISOString().slice(0, 7)
  const currentMonthIncome = income
    .filter(x => x.postedDate.slice(0, 7) === currentMonthKey)
    .reduce((x, y) => x + y.amountMinorUnits, 0)

  return {
    months,
    topTags: topTags.slice(0, 8),
    totalSpend: topTags.reduce((x, y) => x + y.total, 0),
    currentMonthIncome,
    currentMonthKey,
    currentMonthSpend,
    currentMonthLabel: formatFullMonthLabel(currentMonthKey),
    monthChange: currentMonthSpend - previousMonthSpend,
    taggedCoverage: expenses.length > 0 ? Math.round((taggedCount / expenses.length) * 100) : 0,
    timeframeLabel: getTimeframeLabel(months)
  }
}

function analyzeCashFlow(transactions: Transaction[], monthKey: string, accountId: string) {
  const current = transactions.filter(x =>
    x.status.toLowerCase() === 'posted'
    && x.postedDate.slice(0, 7) === monthKey
    && (accountId === 'all' || x.accountId === accountId))

  return {
    income: current.filter(x => x.amountMinorUnits > 0).reduce((x, y) => x + y.amountMinorUnits, 0),
    expenses: current.filter(x => x.amountMinorUnits < 0).reduce((x, y) => x + Math.abs(y.amountMinorUnits), 0)
  }
}

async function getOverviewTransactions() {
  const pageSize = 200
  const pages: Transaction[][] = []

  for (let page = 1; page <= 20; page += 1) {
    const transactions = await api<Transaction[]>(`/api/transactions?page=${page}&pageSize=${pageSize}&sort=postedDate_desc`)
    pages.push(transactions)

    if (transactions.length < pageSize) {
      break
    }
  }

  return pages.flat()
}

function CashFlowRace({ income, expenses }: { income: number; expenses: number }) {
  const total = Math.max(income + expenses, 1)
  const incomeShare = (income / total) * 100
  const expenseShare = (expenses / total) * 100
  const net = income - expenses

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-end">
        <CashFlowSide align="left" color="oklch(0.62 0.14 160)" icon={<CircleDollarSign className="h-4 w-4" />} label="Income" value={currency(income, 'AUD')} />
        <div className="rounded-lg border border-border bg-muted px-4 py-3 text-center sm:min-w-44">
          <p className={net >= 0 ? 'mt-1 text-2xl font-semibold text-primary' : 'mt-1 text-2xl font-semibold text-destructive'}>
            {formatSignedCurrency(net, 'AUD')}
          </p>
        </div>
        <CashFlowSide align="right" color="oklch(0.66 0.19 27)" icon={<ReceiptText className="h-4 w-4" />} label="Expenses" value={currency(expenses, 'AUD')} />
      </div>
      <div className="relative h-12 overflow-hidden rounded-full border border-border bg-muted">
        <div className="absolute inset-y-1 left-1 rounded-l-full transition-all" style={{ width: `calc(${incomeShare}% - 0.25rem)`, backgroundColor: 'oklch(0.62 0.14 160)' }} />
        <div className="absolute inset-y-1 right-1 rounded-r-full transition-all" style={{ width: `calc(${expenseShare}% - 0.25rem)`, backgroundColor: 'oklch(0.66 0.19 27)' }} />
        <div className="absolute inset-y-0 w-px bg-card shadow-[0_0_0_1px_var(--border)] transition-all" style={{ left: `${incomeShare}%` }} />
        <div className="absolute inset-y-0 flex -translate-x-1/2 items-center transition-all" style={{ left: `${incomeShare}%` }}>
          <span className="rounded-full border border-border bg-card px-2 py-1 text-xs font-semibold shadow-sm">VS</span>
        </div>
      </div>
    </div>
  )
}

function CashFlowSide({ align, color, icon, label, value }: { align: 'left' | 'right'; color: string; icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className={align === 'left' ? 'flex items-center gap-3' : 'flex items-center gap-3 sm:flex-row-reverse sm:text-right'}>
      <div className="flex min-w-0 items-center gap-2 font-medium">
        <span className="flex h-8 w-8 items-center justify-center rounded-md text-white" style={{ backgroundColor: color }}>
          {icon}
        </span>
        <span>{label}</span>
      </div>
      <p className="shrink-0 font-semibold">{value}</p>
    </div>
  )
}

function StackedMonthlyBars({ months, tags }: { months: MonthSpend[]; tags: TagSpend[] }) {
  const max = Math.max(...months.map(x => x.total), 1)
  return (
    <div className="space-y-4">
      <div className="grid h-72 items-end gap-3" style={{ gridTemplateColumns: `repeat(${Math.max(months.length, 1)}, minmax(0, 1fr))` }}>
        {months.map(x => (
          <div className="flex h-full flex-col justify-end gap-2" key={x.key}>
            <div className="relative flex h-full flex-col justify-end rounded-md border border-border bg-muted">
              {tags.map(y => {
                const value = x.tags.get(y.id) ?? 0
                return value > 0 ? <MonthlyBarSegment key={y.id} tag={y} value={value} max={max} /> : null
              })}
            </div>
            <div className="text-center">
              <p className="text-xs font-medium text-foreground">{currency(x.total, 'AUD')}</p>
              <p className="text-xs text-muted-foreground">{x.label}</p>
            </div>
          </div>
        ))}
      </div>
      <TagLegend tags={tags} />
    </div>
  )
}

function MonthlyBarSegment({ tag, value, max }: { tag: TagSpend; value: number; max: number }) {
  return (
    <div className="group relative" style={{ backgroundColor: tag.color, height: `${Math.max((value / max) * 100, 3)}%` }}>
      <div className="pointer-events-none absolute left-1/2 top-1/2 z-10 hidden min-w-36 -translate-x-1/2 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block">
        <div className="flex items-center gap-2">
          <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: tag.color }} />
          <span className="truncate font-medium">{tag.name}</span>
        </div>
        <p className="mt-1 font-semibold">{currency(value, 'AUD')}</p>
      </div>
    </div>
  )
}

function ParetoList({ tags, total }: { tags: TagSpend[]; total: number }) {
  const rows = tags.reduce<{ tag: TagSpend; cumulative: number }[]>((x, y) => {
    const previous = x.at(-1)?.cumulative ?? 0
    return [...x, { tag: y, cumulative: previous + y.total }]
  }, [])

  return <div className="space-y-3">{rows.map(x => <ProgressRow key={x.tag.id} color={x.tag.color} label={x.tag.name} value={currency(x.tag.total, 'AUD')} width={total > 0 ? x.tag.total / total : 0} detail={`${Math.round((x.cumulative / Math.max(total, 1)) * 100)}% cumulative`} />)}</div>
}

function ProgressRow({ color, label, value, width, detail, icon }: { color: string; label: string; value: string; width: number; detail: string; icon?: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          {icon}
          <span className="truncate font-medium">{label}</span>
        </div>
        <span className="shrink-0 font-semibold">{value}</span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full" style={{ backgroundColor: color, width: `${Math.max(width * 100, 2)}%` }} />
      </div>
      <p className="text-xs text-muted-foreground">{detail}</p>
    </div>
  )
}

function TagLegend({ tags }: { tags: Pick<TagSpend, 'id' | 'name' | 'color'>[] }) {
  return (
    <div className="flex flex-wrap gap-2">
      {tags.map(x => <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground" key={x.id}><span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: x.color }} />{x.name}</span>)}
    </div>
  )
}

function formatMonthLabel(monthKey: string) {
  return new Date(`${monthKey}-02T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
}

function formatFullMonthLabel(monthKey: string) {
  return new Date(`${monthKey}-02T00:00:00`).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
}

function getTimeframeLabel(months: MonthSpend[]) {
  const firstMonth = months.at(0)
  const lastMonth = months.at(-1)
  if (!firstMonth || !lastMonth) {
    return 'No posted spending data yet'
  }

  const first = new Date(`${firstMonth.key}-02T00:00:00`).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
  const last = new Date(`${lastMonth.key}-02T00:00:00`).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
  return firstMonth.key === lastMonth.key ? first : `${first} to ${last}`
}

function formatSignedCurrency(value: number, code: string) {
  const formatted = currency(Math.abs(value), code)
  return value > 0 ? `+${formatted}` : value < 0 ? `-${formatted}` : formatted
}
