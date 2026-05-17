import { useMemo, useState } from 'react'
import type React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CircleDollarSign, Loader2, ReceiptText } from 'lucide-react'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Overview as OverviewSummary, TransactionTag } from '../lib/types'

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
const internalTransferTagName = 'Internal'

export function Overview() {
  const [overviewAccountId, setOverviewAccountId] = useState('all')
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const overview = useQuery({
    queryKey: ['overview', overviewAccountId],
    queryFn: () => api<OverviewSummary>(`/api/overview${overviewAccountId === 'all' ? '' : `?accountId=${overviewAccountId}`}`)
  })
  const analysis = useMemo(() => mapOverview(overview.data), [overview.data])
  const largestCategoryTags = useMemo(() => getLargestCategoryTags(analysis.topTags), [analysis.topTags])
  const isLoading = accounts.isLoading || overview.isFetching

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <Header title="Overview" subtitle="Tagged spending patterns, trend changes, and unusually large transactions." />
        <select
          aria-label="Overview account"
          className="h-9 rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30 sm:w-64"
          onChange={x => setOverviewAccountId(x.target.value)}
          value={overviewAccountId}
        >
          <option value="all">All accounts</option>
          {(accounts.data ?? []).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
        </select>
      </div>
      {isLoading && <OverviewLoading />}
      <div className="grid gap-4 md:grid-cols-4">
        <Metric label={overviewAccountId === 'all' ? 'Total balance' : 'Account balance'} value={currency(analysis.balance, 'AUD')} />
        <Metric label="This month spent" value={currency(analysis.currentMonthSpend, 'AUD')} />
        <Metric label="Avg daily spend" value={currency(analysis.averageDailySpend, 'AUD')} />
        <Metric label="Tagged coverage" value={`${analysis.taggedCoverage}%`} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Cash flow race</CardTitle>
          <CardDescription>{analysis.currentMonthLabel}</CardDescription>
        </CardHeader>
        <CardContent>
          <CashFlowRace income={analysis.currentMonthIncome} expenses={analysis.currentMonthSpend} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Daily cash flow</CardTitle>
          <CardDescription>{analysis.currentMonthLabel}</CardDescription>
        </CardHeader>
        <CardContent>
          <DailyCashFlowBars days={analysis.dailyCashFlow} />
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
            <ParetoList tags={largestCategoryTags} total={largestCategoryTags.reduce((x, y) => x + y.total, 0)} />
          </CardContent>
        </Card>
      </div>
    </section>
  )
}

function getLargestCategoryTags(tags: TagSpend[]) {
  return tags.filter(x => x.name.toLowerCase() !== internalTransferTagName.toLowerCase() && x.id !== fallbackTag.id)
}

function mapOverview(overview?: OverviewSummary) {
  return {
    balance: overview?.balanceMinorUnits ?? null,
    currentMonthIncome: overview?.currentMonthIncomeMinorUnits ?? 0,
    currentMonthSpend: overview?.currentMonthSpendMinorUnits ?? 0,
    averageDailySpend: overview?.averageDailySpendMinorUnits ?? 0,
    taggedCoverage: overview?.taggedCoverage ?? 0,
    currentMonthKey: overview?.currentMonthKey ?? new Date().toISOString().slice(0, 7),
    currentMonthLabel: overview?.currentMonthLabel ?? 'Loading',
    timeframeLabel: overview?.timeframeLabel ?? 'Loading overview',
    months: (overview?.months ?? []).map(x => ({
      key: x.key,
      label: x.label,
      total: x.totalMinorUnits,
      tags: new Map(x.tags.map(y => [y.tagId, y.amountMinorUnits]))
    })),
    topTags: (overview?.topTags ?? []).map(x => ({
      id: x.id,
      name: x.name,
      color: x.color,
      total: x.totalMinorUnits,
      current: x.currentMinorUnits,
      previous: x.previousMinorUnits,
      months: x.months
    })),
    dailyCashFlow: (overview?.dailyCashFlow ?? []).map(x => ({
      key: x.key,
      day: x.day,
      income: x.incomeMinorUnits,
      expenses: x.expensesMinorUnits
    }))
  }
}

function OverviewLoading() {
  return (
    <div className="flex items-center gap-2 rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground">
      <Loader2 className="h-4 w-4 animate-spin" />
      <span>Loading overview...</span>
    </div>
  )
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
        {months.map((x, index) => (
          <div className="flex h-full flex-col justify-end gap-2" key={x.key}>
            <div className="relative flex h-full flex-col justify-end rounded-md border border-border bg-muted">
              {tags.map(y => {
                const value = x.tags.get(y.id) ?? 0
                return value > 0 ? <MonthlyBarSegment isFirstMonth={index === 0} isLastMonth={index === months.length - 1} key={y.id} tag={y} value={value} max={max} /> : null
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

function DailyCashFlowBars({ days }: { days: { key: string; day: number; income: number; expenses: number }[] }) {
  const max = Math.max(...days.map(x => x.income + x.expenses), 1)
  return (
    <div className="space-y-4">
      <div className="grid h-80 items-end gap-1" style={{ gridTemplateColumns: `repeat(${Math.max(days.length, 1)}, minmax(0, 1fr))` }}>
        {days.map((x, index) => {
          const incomeHeight = (x.income / max) * 100
          const expenseHeight = (x.expenses / max) * 100
          const tooltipPosition = index === 0
            ? 'left-0'
            : index === days.length - 1
              ? 'right-0'
              : 'left-1/2 -translate-x-1/2'
          return (
            <div className="group relative flex h-full min-w-0 flex-col justify-end gap-1" key={x.key}>
              <div className="flex h-full flex-col justify-end overflow-hidden rounded-sm border border-border bg-muted">
                {x.income > 0 && <div className="shrink-0 bg-[oklch(0.62_0.14_160)]" style={{ height: `${Math.max(incomeHeight, 3)}%` }} />}
                {x.expenses > 0 && <div className="shrink-0 bg-[oklch(0.66_0.19_27)]" style={{ height: `${Math.max(expenseHeight, 3)}%` }} />}
                <div className={`pointer-events-none absolute top-1/2 z-10 hidden min-w-40 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block ${tooltipPosition}`}>
                  <p className="font-medium">Day {x.day}</p>
                  <div className="mt-1 flex items-center gap-2">
                    <span className="h-2.5 w-2.5 shrink-0 rounded-full bg-[oklch(0.62_0.14_160)]" />
                    <span>Income</span>
                    <span className="ml-auto font-semibold">{currency(x.income, 'AUD')}</span>
                  </div>
                  <div className="mt-1 flex items-center gap-2">
                    <span className="h-2.5 w-2.5 shrink-0 rounded-full bg-[oklch(0.66_0.19_27)]" />
                    <span>Spend</span>
                    <span className="ml-auto font-semibold">{currency(x.expenses, 'AUD')}</span>
                  </div>
                </div>
              </div>
              <p className="text-center text-[10px] text-muted-foreground">{x.day}</p>
            </div>
          )
        })}
      </div>
      <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-full bg-[oklch(0.62_0.14_160)]" />Income</span>
        <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-full bg-[oklch(0.66_0.19_27)]" />Spend</span>
      </div>
    </div>
  )
}

function MonthlyBarSegment({ isFirstMonth, isLastMonth, tag, value, max }: { isFirstMonth: boolean; isLastMonth: boolean; tag: TagSpend; value: number; max: number }) {
  const tooltipPosition = isFirstMonth
    ? 'left-0'
    : isLastMonth
      ? 'right-0'
      : 'left-1/2 -translate-x-1/2'

  return (
    <div className="group relative" style={{ backgroundColor: tag.color, height: `${Math.max((value / max) * 100, 3)}%` }}>
      <div className={`pointer-events-none absolute top-1/2 z-10 hidden min-w-36 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block ${tooltipPosition}`}>
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

function formatSignedCurrency(value: number, code: string) {
  const formatted = currency(Math.abs(value), code)
  return value > 0 ? `+${formatted}` : value < 0 ? `-${formatted}` : formatted
}
