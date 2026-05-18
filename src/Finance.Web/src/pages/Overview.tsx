import { useMemo, useState } from 'react'
import type React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CircleDollarSign, LineChart, Loader2, ReceiptText, X } from 'lucide-react'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { Button } from '../components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Overview as OverviewSummary, OverviewDailyCashFlow, OverviewMetricSnapshot, TransactionTag } from '../lib/types'

type DailyCashFlowRange = '1w' | '1m' | '3m'

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
const dailyCashFlowRanges: { value: DailyCashFlowRange; label: string; description: string }[] = [
  { value: '1w', label: '1W', description: 'Last 7 days' },
  { value: '1m', label: '1M', description: 'Last month' },
  { value: '3m', label: '3M', description: 'Last 3 months' }
]

export function Overview() {
  const [overviewAccountId, setOverviewAccountId] = useState('all')
  const [includeInternalTransfers, setIncludeInternalTransfers] = useState(true)
  const [dailyCashFlowRange, setDailyCashFlowRange] = useState<DailyCashFlowRange>('1m')
  const [showAverageDailySpendHistory, setShowAverageDailySpendHistory] = useState(false)
  const isAllAccounts = overviewAccountId === 'all'
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const overview = useQuery({
    queryKey: ['overview', overviewAccountId, includeInternalTransfers],
    queryFn: () => {
      const params = new URLSearchParams()
      if (!isAllAccounts) {
        params.set('accountId', overviewAccountId)
        params.set('includeInternalTransfers', `${includeInternalTransfers}`)
      }

      return api<OverviewSummary>(`/api/overview${params.size === 0 ? '' : `?${params}`}`)
    }
  })
  const dailyCashFlow = useQuery({
    placeholderData: x => x,
    queryKey: ['daily-cash-flow', overviewAccountId, includeInternalTransfers, dailyCashFlowRange],
    queryFn: () => {
      const params = new URLSearchParams({ range: dailyCashFlowRange })
      if (!isAllAccounts) {
        params.set('accountId', overviewAccountId)
        params.set('includeInternalTransfers', `${includeInternalTransfers}`)
      }

      return api<OverviewDailyCashFlow[]>(`/api/overview/daily-cash-flow?${params}`)
    }
  })
  const averageDailySpendHistory = useQuery({
    enabled: showAverageDailySpendHistory,
    placeholderData: x => x,
    queryKey: ['average-daily-spend-history', overviewAccountId, includeInternalTransfers],
    queryFn: () => {
      const params = new URLSearchParams()
      if (!isAllAccounts) {
        params.set('accountId', overviewAccountId)
        params.set('includeInternalTransfers', `${includeInternalTransfers}`)
      }

      return api<OverviewMetricSnapshot[]>(`/api/overview/average-daily-spend-history${params.size === 0 ? '' : `?${params}`}`)
    }
  })
  const analysis = useMemo(() => mapOverview(overview.data), [overview.data])
  const dailyCashFlowDays = useMemo(() => mapDailyCashFlow(dailyCashFlow.data ?? overview.data?.dailyCashFlow), [dailyCashFlow.data, overview.data?.dailyCashFlow])
  const averageDailySpendTrend = useMemo(() => mapAverageDailySpendHistory(averageDailySpendHistory.data), [averageDailySpendHistory.data])
  const largestCategoryTags = useMemo(() => getLargestCategoryTags(analysis.topTags), [analysis.topTags])
  const isLoading = accounts.isLoading || overview.isLoading

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <Header title="Overview" subtitle="Tagged spending patterns, trend changes, and unusually large transactions." />
        <div className="flex flex-col gap-2 sm:items-end">
          <select
            aria-label="Overview account"
            className="h-9 rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30 sm:w-64"
            onChange={x => setOverviewAccountId(x.target.value)}
            value={overviewAccountId}
          >
            <option value="all">All accounts</option>
            {(accounts.data ?? []).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
          </select>
          <label className={isAllAccounts ? 'inline-flex items-center gap-2 text-sm text-muted-foreground opacity-60' : 'inline-flex items-center gap-2 text-sm text-muted-foreground'}>
            <input
              checked={!isAllAccounts && includeInternalTransfers}
              className="size-4 rounded border-input accent-primary disabled:cursor-not-allowed"
              disabled={isAllAccounts}
              onChange={x => setIncludeInternalTransfers(x.target.checked)}
              type="checkbox"
            />
            Include internal payments
          </label>
        </div>
      </div>
      {isLoading && <OverviewLoading />}
      <div className="grid gap-4 md:grid-cols-4">
        <Metric label={isAllAccounts ? 'Total balance' : 'Account balance'} value={currency(analysis.balance, 'AUD')} />
        <Metric label="This month spent" value={currency(analysis.currentMonthSpend, 'AUD')} />
        <Metric
          action={
            <Button
              aria-label={showAverageDailySpendHistory ? 'Hide average daily spend trend' : 'Show average daily spend trend'}
              onClick={() => setShowAverageDailySpendHistory(x => !x)}
              size="icon"
              title={showAverageDailySpendHistory ? 'Hide trend' : 'Show trend'}
              type="button"
              variant={showAverageDailySpendHistory ? 'secondary' : 'ghost'}
            >
              {showAverageDailySpendHistory ? <X className="h-4 w-4" /> : <LineChart className="h-4 w-4" />}
            </Button>
          }
          label="Avg daily spend"
          value={currency(analysis.averageDailySpend, 'AUD')}
        />
        <Metric label="Tagged coverage" value={`${analysis.taggedCoverage}%`} />
      </div>

      {showAverageDailySpendHistory && (
        <Card>
          <CardHeader>
            <CardTitle>Avg daily spend trend</CardTitle>
            <CardDescription>{averageDailySpendTrend.length > 0 ? `${formatDailyCashFlowDate(averageDailySpendTrend[0].key)} to ${formatDailyCashFlowDate(averageDailySpendTrend.at(-1)?.key ?? averageDailySpendTrend[0].key)}` : 'Loading trend'}</CardDescription>
          </CardHeader>
          <CardContent>
            <AverageDailySpendTrend points={averageDailySpendTrend} isLoading={averageDailySpendHistory.isFetching} />
          </CardContent>
        </Card>
      )}

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
          <div>
            <CardTitle>Daily cash flow</CardTitle>
            <CardDescription>{dailyCashFlowRanges.find(x => x.value === dailyCashFlowRange)?.description}</CardDescription>
          </div>
          <CardAction className="inline-flex rounded-md border border-border bg-muted p-1">
            {dailyCashFlowRanges.map(x => (
              <Button
                aria-pressed={dailyCashFlowRange === x.value}
                className={dailyCashFlowRange === x.value ? 'bg-background shadow-sm hover:bg-background' : 'text-muted-foreground'}
                key={x.value}
                onClick={() => setDailyCashFlowRange(x.value)}
                size="sm"
                type="button"
                variant="ghost"
              >
                {x.label}
              </Button>
            ))}
          </CardAction>
        </CardHeader>
        <CardContent>
          <DailyCashFlowBars days={dailyCashFlowDays} isLoading={dailyCashFlow.isFetching} />
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
    dailyCashFlow: mapDailyCashFlow(overview?.dailyCashFlow)
  }
}

function mapDailyCashFlow(days?: OverviewDailyCashFlow[]) {
  return (days ?? []).map(x => ({
    key: x.key,
    day: x.day,
    income: x.incomeMinorUnits,
    expenses: x.expensesMinorUnits
  }))
}

function mapAverageDailySpendHistory(days?: OverviewMetricSnapshot[]) {
  return (days ?? []).map(x => ({
    key: x.key,
    value: x.averageDailySpendMinorUnits
  }))
}

function OverviewLoading() {
  return (
    <div className="fixed right-4 top-4 z-50 flex items-center gap-2 rounded-md border border-border bg-popover px-3 py-2 text-sm text-popover-foreground shadow-lg">
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

function DailyCashFlowBars({ days, isLoading }: { days: { key: string; day: number; income: number; expenses: number }[]; isLoading: boolean }) {
  const max = Math.max(...days.map(x => x.income + x.expenses), 1)
  return (
    <div className="space-y-4">
      <div className={isLoading ? 'grid h-80 items-end gap-1 opacity-60 transition-opacity' : 'grid h-80 items-end gap-1 transition-opacity'} style={{ gridTemplateColumns: `repeat(${Math.max(days.length, 1)}, minmax(0, 1fr))` }}>
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
                  <p className="font-medium">{formatDailyCashFlowDate(x.key)}</p>
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
              <p className="text-center text-[10px] text-muted-foreground">{formatDailyCashFlowTick(x.key, days.length)}</p>
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

function AverageDailySpendTrend({ points, isLoading }: { points: { key: string; value: number }[]; isLoading: boolean }) {
  const width = 720
  const height = 220
  const padding = 20
  const max = Math.max(...points.map(x => x.value), 1)
  const chartPoints = points.map((x, index) => {
    const left = points.length <= 1 ? padding : padding + (index / (points.length - 1)) * (width - padding * 2)
    const top = height - padding - (x.value / max) * (height - padding * 2)
    const tooltipPosition = index === 0
      ? 'left-0'
      : index === points.length - 1
        ? 'right-0'
        : 'left-1/2 -translate-x-1/2'
    return { ...x, left, top, tooltipPosition }
  })
  const path = chartPoints.map((x, index) => {
    return `${index === 0 ? 'M' : 'L'} ${x.left} ${x.top}`
  }).join(' ')

  return (
    <div className={isLoading ? 'space-y-3 opacity-60 transition-opacity' : 'space-y-3 transition-opacity'}>
      <div className="relative h-64 overflow-hidden rounded-md border border-border bg-muted">
        <svg className="h-full w-full" preserveAspectRatio="none" viewBox={`0 0 ${width} ${height}`}>
          <path d={`M ${padding} ${height - padding} H ${width - padding}`} fill="none" stroke="currentColor" strokeOpacity="0.15" />
          {path && <path d={path} fill="none" stroke="oklch(0.62 0.14 160)" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" vectorEffect="non-scaling-stroke" />}
          {chartPoints.map(x => (
            <circle cx={x.left} cy={x.top} fill="var(--background)" key={x.key} r="3.5" stroke="oklch(0.62 0.14 160)" strokeWidth="2" vectorEffect="non-scaling-stroke" />
          ))}
        </svg>
        {chartPoints.map(x => (
          <div
            className="group absolute z-10 h-9 w-9 -translate-x-1/2 -translate-y-1/2"
            key={x.key}
            style={{ left: `${(x.left / width) * 100}%`, top: `${(x.top / height) * 100}%` }}
          >
            <div className="h-full w-full rounded-full" />
            <div className={`pointer-events-none absolute top-1/2 hidden min-w-40 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block ${x.tooltipPosition}`}>
              <p className="font-medium">{formatDailyCashFlowDate(x.key)}</p>
              <div className="mt-1 flex items-center gap-2">
                <span className="h-2.5 w-2.5 shrink-0 rounded-full bg-[oklch(0.62_0.14_160)]" />
                <span>Avg daily spend</span>
                <span className="ml-auto font-semibold">{currency(x.value, 'AUD')}</span>
              </div>
            </div>
          </div>
        ))}
      </div>
      <div className="flex items-center justify-between gap-3 text-xs text-muted-foreground">
        <span>{points[0] ? currency(points[0].value, 'AUD') : currency(0, 'AUD')}</span>
        <span>{points.at(-1) ? currency(points.at(-1)!.value, 'AUD') : currency(0, 'AUD')}</span>
      </div>
    </div>
  )
}

function formatDailyCashFlowDate(key: string) {
  return new Date(`${key}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

function formatDailyCashFlowTick(key: string, days: number) {
  if (days <= 31) {
    return new Date(`${key}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
  }

  return ''
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
