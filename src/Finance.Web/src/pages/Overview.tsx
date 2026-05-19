import { useMemo, useState } from 'react'
import type React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CircleDollarSign, LineChart, Loader2, ReceiptText, X } from 'lucide-react'
import { Header } from '../components/Header'
import { Metric } from '../components/Metric'
import { Button } from '../components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, Overview as OverviewSummary, OverviewDailyCashFlow, OverviewMetricSnapshot, SavingsTrajectory as SavingsTrajectorySummary, TransactionTag } from '../lib/types'

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
    placeholderData: keepPreviousData,
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
    enabled: dailyCashFlowRange !== '1m',
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
  const savingsTrajectory = useQuery({
    enabled: !isAllAccounts,
    placeholderData: x => x,
    queryKey: ['savings-trajectory', overviewAccountId],
    queryFn: () => api<SavingsTrajectorySummary>(`/api/accounts/${overviewAccountId}/savings-trajectory`)
  })
  const analysis = useMemo(() => mapOverview(overview.data), [overview.data])
  const dailyCashFlowDays = useMemo(() => mapDailyCashFlow(dailyCashFlowRange === '1m' ? overview.data?.dailyCashFlow : dailyCashFlow.data), [dailyCashFlow.data, dailyCashFlowRange, overview.data?.dailyCashFlow])
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

      {!isAllAccounts && (
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Savings trajectory</CardTitle>
              <CardDescription>{getSelectedAccountName(accounts.data, overviewAccountId)}</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <SavingsTrajectoryChart trajectory={savingsTrajectory.data} isLoading={savingsTrajectory.isFetching} />
          </CardContent>
        </Card>
      )}

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

function getSelectedAccountName(accounts: Account[] | undefined, accountId: string) {
  return accounts?.find(x => x.id === accountId)?.displayName ?? 'Selected account'
}

function OverviewLoading() {
  return (
    <div className="fixed right-4 top-4 z-50 flex items-center gap-2 rounded-md border border-border bg-popover px-3 py-2 text-sm text-popover-foreground shadow-lg">
      <Loader2 className="h-4 w-4 animate-spin" />
      <span>Loading overview...</span>
    </div>
  )
}

function SavingsTrajectoryChart({ trajectory, isLoading }: { trajectory?: SavingsTrajectorySummary; isLoading: boolean }) {
  const width = 720
  const height = 260
  const padding = { top: 22, right: 72, bottom: 34, left: 72 }
  const actual = trajectory?.actual ?? []
  const projection = trajectory?.projection ?? []
  const allPoints = [...actual, ...projection]
  const bandPoints = projection.map(x => {
    const startingBalance = actual.at(-1)?.balanceMinorUnits ?? 0
    const projectedGrowth = x.balanceMinorUnits - startingBalance
    return {
      key: x.key,
      lowerBalanceMinorUnits: startingBalance + Math.round(projectedGrowth * 0.75),
      upperBalanceMinorUnits: startingBalance + Math.round(projectedGrowth * 1.25)
    }
  })
  const max = Math.max(...allPoints.map(x => x.balanceMinorUnits), ...bandPoints.map(x => x.upperBalanceMinorUnits), 1)
  const min = Math.min(...allPoints.map(x => x.balanceMinorUnits), ...bandPoints.map(x => x.lowerBalanceMinorUnits), 0)
  const range = Math.max(max - min, 1)
  const totalPoints = Math.max(allPoints.length, 1)
  const actualPoints = actual.map((x, index) => toTrajectoryChartPoint(x, index, totalPoints, width, height, padding, min, range))
  const projectionPoints = projection.map((x, index) => toTrajectoryChartPoint(x, actual.length + index, totalPoints, width, height, padding, min, range))
  const lowerBandPoints = bandPoints.map((x, index) => toTrajectoryChartPoint({ key: x.key, balanceMinorUnits: x.lowerBalanceMinorUnits, depositMinorUnits: 0, interestMinorUnits: 0, withdrawalMinorUnits: 0 }, actual.length + index, totalPoints, width, height, padding, min, range))
  const upperBandPoints = bandPoints.map((x, index) => toTrajectoryChartPoint({ key: x.key, balanceMinorUnits: x.upperBalanceMinorUnits, depositMinorUnits: 0, interestMinorUnits: 0, withdrawalMinorUnits: 0 }, actual.length + index, totalPoints, width, height, padding, min, range))
  const actualPath = toStepPath(actualPoints)
  const projectionStart = actualPoints.at(-1)
  const projectionPath = toSmoothPath(projectionStart ? [projectionStart, ...projectionPoints] : projectionPoints)
  const bandPath = projectionStart && lowerBandPoints.length > 0 && upperBandPoints.length > 0
    ? toBandPath([projectionStart, ...upperBandPoints], [...lowerBandPoints].reverse().concat(projectionStart))
    : ''
  const todayLeft = projectionStart?.left
  const finalPoint = projectionPoints.at(-1)
  const projectedGrowth = finalPoint && projectionStart ? finalPoint.balanceMinorUnits - projectionStart.balanceMinorUnits : 0
  const currencyCode = trajectory?.currency ?? 'AUD'
  const yTicks = getCurrencyTicks(min, max, 4)
  const monthTicks = getMonthTicks(allPoints).map(x => {
    const index = allPoints.findIndex(y => y.key === x.key)
    const left = index < 0 ? padding.left : toTrajectoryLeft(index, totalPoints, width, padding)
    return { ...x, left }
  })
  const hoverPoints = [
    ...actualPoints.filter(x => x.depositMinorUnits > 0 || x.interestMinorUnits > 0 || x.withdrawalMinorUnits > 0),
    ...projectionPoints.filter((_x, index) => (index + 1) % 30 === 0 || index === projectionPoints.length - 1)
  ]

  if (!trajectory && !isLoading) {
    return <p className="text-sm text-muted-foreground">No savings transactions found for this account yet.</p>
  }

  return (
    <div className={isLoading ? 'space-y-4 opacity-60 transition-opacity' : 'space-y-4 transition-opacity'}>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <SavingsMetric label="Deposits" detail="Historical, excluding interest" value={currency(trajectory?.totalDepositsMinorUnits ?? 0, currencyCode)} color="oklch(0.62 0.14 160)" />
        <SavingsMetric label="Interest" detail="Bonus and credit interest" value={currency(trajectory?.totalInterestMinorUnits ?? 0, currencyCode)} color="oklch(0.7 0.13 85)" />
        <SavingsMetric label="Projected deposits" detail="Next 30 days" value={currency(trajectory?.projectedMonthlyDepositsMinorUnits ?? 0, currencyCode)} color="oklch(0.5 0.18 250)" />
        <SavingsMetric label="Projected interest" detail="Next 30 days, compounding" value={currency(trajectory?.projectedMonthlyInterestMinorUnits ?? 0, currencyCode)} color="oklch(0.56 0.15 305)" />
      </div>
      <div className="relative h-[22rem]">
        <div className="absolute inset-0 overflow-hidden rounded-md border border-border bg-muted">
          <svg className="h-full w-full" preserveAspectRatio="none" viewBox={`0 0 ${width} ${height}`}>
            {yTicks.map(x => {
              const top = toTrajectoryTop(x, height, padding, min, range)
              return <g key={x}>
                <path d={`M ${padding.left} ${top} H ${width - padding.right}`} fill="none" stroke="currentColor" strokeOpacity="0.08" />
                <text fill="currentColor" fontSize="11" opacity="0.55" x={padding.left - 10} y={top + 4} textAnchor="end">{formatCompactCurrency(x, currencyCode)}</text>
              </g>
            })}
            {monthTicks.map(x => (
              <g key={x.key}>
                <path d={`M ${x.left} ${padding.top} V ${height - padding.bottom}`} fill="none" stroke="currentColor" strokeOpacity="0.05" />
                <text fill="currentColor" fontSize="11" opacity="0.55" x={x.left} y={height - 10} textAnchor="middle">{x.label}</text>
              </g>
            ))}
            {todayLeft && <path d={`M ${todayLeft} ${padding.top} V ${height - padding.bottom}`} fill="none" stroke="currentColor" strokeDasharray="4 5" strokeOpacity="0.35" />}
            {bandPath && <path d={bandPath} fill="oklch(0.5 0.18 250 / 0.16)" stroke="none" />}
            {actualPath && <path d={actualPath} fill="none" stroke="oklch(0.62 0.14 160)" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" vectorEffect="non-scaling-stroke" />}
            {projectionPath && <path d={projectionPath} fill="none" stroke="oklch(0.5 0.18 250)" strokeDasharray="7 7" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" vectorEffect="non-scaling-stroke" />}
            {todayLeft && <text fill="currentColor" fontSize="11" opacity="0.62" x={todayLeft + 8} y={padding.top + 14}>Today</text>}
          </svg>
        </div>
        {finalPoint && (
          <div
            className="pointer-events-none absolute z-10 max-w-48 rounded-md border border-border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-lg"
            style={{ left: `${Math.min(78, Math.max(48, (finalPoint.left / width) * 100))}%`, top: `${Math.max(8, ((finalPoint.top / height) * 100) - 8)}%` }}
          >
            <p className="font-medium">6-month projection</p>
            <p className="mt-1 text-base font-semibold">{currency(finalPoint.balanceMinorUnits, currencyCode)}</p>
            <p className="mt-1 text-muted-foreground">{formatSignedCurrency(projectedGrowth, currencyCode)} projected growth</p>
          </div>
        )}
        {[...actualPoints, ...projectionPoints].map(x => <SavingsHoverPoint currencyCode={currencyCode} key={`${x.key}-${x.left}`} point={x} showMarker={false} />)}
        {hoverPoints.map(x => <SavingsHoverPoint currencyCode={currencyCode} key={`marker-${x.key}-${x.left}`} point={x} showMarker />)}
      </div>
      <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-full bg-[oklch(0.62_0.14_160)]" />History</span>
        <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-5 border-t-2 border-dashed border-[oklch(0.5_0.18_250)]" />6-month projection</span>
        <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-5 rounded-sm bg-[oklch(0.5_0.18_250_/_0.18)]" />Deposit variance band</span>
      </div>
    </div>
  )
}

function SavingsHoverPoint({ currencyCode, point, showMarker }: { currencyCode: string; point: SavingsChartPoint; showMarker: boolean }) {
  return (
    <div className={showMarker ? 'group absolute z-30 h-10 w-10 -translate-x-1/2 -translate-y-1/2' : 'group absolute z-20 h-10 w-10 -translate-x-1/2 -translate-y-1/2'} style={{ left: `${(point.left / 720) * 100}%`, top: `${(point.top / 260) * 100}%` }}>
      <div className={showMarker ? 'h-full w-full rounded-full' : 'h-full w-full rounded-full'} />
      <div className={`pointer-events-none absolute top-1/2 z-50 hidden min-w-48 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block ${point.tooltipPosition}`}>
        <p className="font-medium">{formatDailyCashFlowDate(point.key)}</p>
        <p className="mt-1 font-semibold">{currency(point.balanceMinorUnits, currencyCode)}</p>
        {point.depositMinorUnits > 0 && <p className="mt-1 text-muted-foreground">Deposit {currency(point.depositMinorUnits, currencyCode)}</p>}
        {point.interestMinorUnits > 0 && <p className="mt-1 text-muted-foreground">Interest {currency(point.interestMinorUnits, currencyCode)}</p>}
        {point.withdrawalMinorUnits > 0 && <p className="mt-1 text-muted-foreground">Withdrawal {currency(point.withdrawalMinorUnits, currencyCode)}</p>}
        {point.depositMinorUnits === 0 && point.interestMinorUnits === 0 && point.withdrawalMinorUnits === 0 && <p className="mt-1 text-muted-foreground">No movement</p>}
      </div>
    </div>
  )
}

function SavingsMetric({ color, detail, label, value }: { color: string; detail: string; label: string; value: string }) {
  return (
    <div className="rounded-md border border-border bg-muted/50 p-3">
      <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
        <span>{label}</span>
      </div>
      <p className="mt-2 text-lg font-semibold">{value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{detail}</p>
    </div>
  )
}

function toTrajectoryChartPoint(point: { key: string; balanceMinorUnits: number; depositMinorUnits: number; interestMinorUnits: number; withdrawalMinorUnits: number }, index: number, total: number, width: number, height: number, padding: ChartPadding, min: number, range: number) {
  const left = toTrajectoryLeft(index, total, width, padding)
  const top = toTrajectoryTop(point.balanceMinorUnits, height, padding, min, range)
  const tooltipPosition = index === 0
    ? 'left-0'
    : index === total - 1
      ? 'right-0'
      : 'left-1/2 -translate-x-1/2'
  return { ...point, left, top, tooltipPosition }
}

type SavingsChartPoint = ReturnType<typeof toTrajectoryChartPoint>

type ChartPadding = {
  top: number
  right: number
  bottom: number
  left: number
}

function toTrajectoryLeft(index: number, total: number, width: number, padding: ChartPadding) {
  return total <= 1 ? padding.left : padding.left + (index / (total - 1)) * (width - padding.left - padding.right)
}

function toTrajectoryTop(value: number, height: number, padding: ChartPadding, min: number, range: number) {
  return height - padding.bottom - ((value - min) / range) * (height - padding.top - padding.bottom)
}

function toSmoothPath(points: { left: number; top: number }[]) {
  if (points.length <= 1) {
    return points.length === 1 ? `M ${points[0].left} ${points[0].top}` : ''
  }

  return points.reduce((path, point, index) => {
    if (index === 0) {
      return `M ${point.left} ${point.top}`
    }

    const previous = points[index - 1]
    const controlX = (previous.left + point.left) / 2
    return `${path} C ${controlX} ${previous.top}, ${controlX} ${point.top}, ${point.left} ${point.top}`
  }, '')
}

function toStepPath(points: { left: number; top: number }[]) {
  if (points.length <= 1) {
    return points.length === 1 ? `M ${points[0].left} ${points[0].top}` : ''
  }

  return points.reduce((path, point, index) => {
    if (index === 0) {
      return `M ${point.left} ${point.top}`
    }

    const previous = points[index - 1]
    return `${path} L ${point.left} ${previous.top} L ${point.left} ${point.top}`
  }, '')
}

function toBandPath(upperPoints: { left: number; top: number }[], lowerPoints: { left: number; top: number }[]) {
  const upperPath = toSmoothPath(upperPoints)
  const lowerPath = lowerPoints.map((x, index) => `${index === 0 ? 'L' : 'L'} ${x.left} ${x.top}`).join(' ')
  return upperPath && lowerPath ? `${upperPath} ${lowerPath} Z` : ''
}

function getCurrencyTicks(min: number, max: number, count: number) {
  const range = Math.max(max - min, 1)
  return Array.from({ length: count }, (_x, index) => Math.round(min + (range / (count - 1)) * index))
}

function getMonthTicks(points: { key: string }[]) {
  const ticks = new Map<string, { key: string; label: string }>()
  for (const point of points) {
    const monthKey = point.key.slice(0, 7)
    if (!ticks.has(monthKey)) {
      ticks.set(monthKey, {
        key: point.key,
        label: new Date(`${point.key}T00:00:00`).toLocaleDateString(undefined, { month: 'short' })
      })
    }
  }

  return [...ticks.values()]
}

function formatCompactCurrency(value: number, code: string) {
  return new Intl.NumberFormat(undefined, { currency: code, maximumFractionDigits: 0, notation: 'compact', style: 'currency' }).format(value / 100)
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
      <div className="relative h-64">
        <div className="absolute inset-0 overflow-hidden rounded-md border border-border bg-muted">
          <svg className="h-full w-full" preserveAspectRatio="none" viewBox={`0 0 ${width} ${height}`}>
            <path d={`M ${padding} ${height - padding} H ${width - padding}`} fill="none" stroke="currentColor" strokeOpacity="0.15" />
            {path && <path d={path} fill="none" stroke="oklch(0.62 0.14 160)" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" vectorEffect="non-scaling-stroke" />}
          </svg>
        </div>
        {chartPoints.map(x => (
          <div
            className="group absolute z-20 h-9 w-9 -translate-x-1/2 -translate-y-1/2"
            key={x.key}
            style={{ left: `${(x.left / width) * 100}%`, top: `${(x.top / height) * 100}%` }}
          >
            <div className="h-full w-full rounded-full" />
            <div className={`pointer-events-none absolute top-1/2 z-30 hidden min-w-40 -translate-y-1/2 rounded-md border border-border bg-popover px-3 py-2 text-left text-xs text-popover-foreground shadow-lg group-hover:block ${x.tooltipPosition}`}>
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
