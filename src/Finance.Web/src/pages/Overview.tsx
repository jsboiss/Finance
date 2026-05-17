import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
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
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const transactions = useQuery({ queryKey: ['transactions', 'overview'], queryFn: () => api<Transaction[]>('/api/transactions?pageSize=1000&sort=postedDate_desc') })
  const availableBalances = accounts.data?.filter(x => x.currentBalanceMinorUnits != null) ?? []
  const totalBalance = availableBalances.length > 0 ? availableBalances.reduce((x, y) => x + (y.currentBalanceMinorUnits ?? 0), 0) : null
  const analysis = useMemo(() => analyzeSpending(transactions.data ?? []), [transactions.data])

  return (
    <section className="space-y-6">
      <Header title="Overview" subtitle="Tagged spending patterns, trend changes, and unusually large transactions." />
      <div className="grid gap-4 md:grid-cols-4">
        <Metric label="Total balance" value={currency(totalBalance, 'AUD')} />
        <Metric label="This month spent" value={currency(analysis.currentMonthSpend, 'AUD')} />
        <Metric label="Month change" value={formatSignedCurrency(analysis.monthChange, 'AUD')} />
        <Metric label="Tagged coverage" value={`${analysis.taggedCoverage}%`} />
      </div>

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
            <CardTitle>Largest categories</CardTitle>
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
  const expenses = transactions.filter(x => x.amountMinorUnits < 0 && x.status.toLowerCase() === 'posted')
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
    if (transaction.tags.length > 0) {
      taggedCount += 1
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
      const month = monthMap.get(transaction.postedDate.slice(0, 7))
      const monthIndex = monthKeys.indexOf(transaction.postedDate.slice(0, 7))
      tagSpend.total += amount

      if (month && monthIndex >= 0) {
        month.total += amount
        month.tags.set(tag.id, (month.tags.get(tag.id) ?? 0) + amount)
        tagSpend.months[monthIndex] += amount
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
  return {
    months,
    topTags: topTags.slice(0, 8),
    totalSpend: topTags.reduce((x, y) => x + y.total, 0),
    currentMonthSpend,
    monthChange: currentMonthSpend - previousMonthSpend,
    taggedCoverage: expenses.length > 0 ? Math.round((taggedCount / expenses.length) * 100) : 0,
    timeframeLabel: getTimeframeLabel(months)
  }
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
  let cumulative = 0
  return <div className="space-y-3">{tags.map(x => {
    cumulative += x.total
    return <ProgressRow key={x.id} color={x.color} label={x.name} value={currency(x.total, 'AUD')} width={total > 0 ? x.total / total : 0} detail={`${Math.round((cumulative / Math.max(total, 1)) * 100)}% cumulative`} />
  })}</div>
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
