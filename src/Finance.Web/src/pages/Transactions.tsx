import { useQuery } from '@tanstack/react-query'
import { createColumnHelper, flexRender, getCoreRowModel, getFilteredRowModel, type ColumnFiltersState, useReactTable } from '@tanstack/react-table'
import { SlidersHorizontal, X } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Header } from '../components/Header'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Transaction } from '../lib/types'

type DateFilter = {
  from?: string
  to?: string
}

type AmountFilter = {
  min?: string
  max?: string
}

export function Transactions() {
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [showFilters, setShowFilters] = useState(false)
  const transactions = useQuery({ queryKey: ['transactions'], queryFn: () => api<Transaction[]>('/api/transactions?pageSize=100') })
  const helper = createColumnHelper<Transaction>()
  const table = useReactTable({
    data: transactions.data ?? [],
    columns: [
      helper.accessor('postedDate', {
        header: 'Date',
        filterFn: (x, y, z: DateFilter) => {
          const value = x.getValue<string>(y)
          return (!z.from || value >= z.from) && (!z.to || value <= z.to)
        }
      }),
      helper.accessor('accountDisplayName', {
        header: 'Account',
        filterFn: 'includesString'
      }),
      helper.accessor('description', {
        header: 'Description',
        cell: x => (
          <div>
            <p>{x.getValue()}</p>
            {x.row.original.merchantName && <p className="text-xs text-muted-foreground">{x.row.original.merchantName}</p>}
          </div>
        ),
        filterFn: 'includesString'
      }),
      helper.accessor('category', {
        header: 'Category',
        filterFn: 'includesString'
      }),
      helper.accessor('amountMinorUnits', {
        header: 'Amount',
        cell: x => currency(x.getValue(), x.row.original.currency),
        filterFn: (x, y, z: AmountFilter) => {
          const value = x.getValue<number>(y) / 100
          const min = z.min ? Number(z.min) : null
          const max = z.max ? Number(z.max) : null
          return (min == null || value >= min) && (max == null || value <= max)
        }
      })
    ],
    state: { columnFilters },
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel()
  })
  const hasFilters = columnFilters.length > 0

  return (
    <section className="space-y-6">
      <Header title="Transactions" subtitle="Posted transactions only, ready for filtering and reconciliation checks." />
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Showing {table.getRowModel().rows.length} of {transactions.data?.length ?? 0} transactions
        </p>
        <div className="flex gap-2">
          <Button onClick={() => setShowFilters(x => !x)} size="sm" variant={showFilters ? 'secondary' : 'outline'}>
            <SlidersHorizontal data-icon="inline-start" />
            Filters
          </Button>
          <Button disabled={!hasFilters} onClick={() => table.resetColumnFilters()} size="sm" variant="outline">
            <X data-icon="inline-start" />
            Clear
          </Button>
        </div>
      </div>
      {showFilters && (
        <Card className="grid gap-4 p-4 md:grid-cols-2 xl:grid-cols-5">
          <FilterField className="xl:col-span-2" label="Date">
            <DateRangeFilter
              value={(table.getColumn('postedDate')?.getFilterValue() as DateFilter | undefined) ?? {}}
              onChange={x => table.getColumn('postedDate')?.setFilterValue(x.from || x.to ? x : undefined)}
            />
          </FilterField>
          <FilterField label="Account">
            <input
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
              onChange={x => table.getColumn('accountDisplayName')?.setFilterValue(x.target.value)}
              placeholder="Search accounts"
              value={(table.getColumn('accountDisplayName')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField label="Description">
            <input
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
              onChange={x => table.getColumn('description')?.setFilterValue(x.target.value)}
              placeholder="Search descriptions"
              value={(table.getColumn('description')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField label="Category">
            <input
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
              onChange={x => table.getColumn('category')?.setFilterValue(x.target.value)}
              placeholder="Search categories"
              value={(table.getColumn('category')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField className="xl:col-span-2" label="Amount">
            <AmountRangeFilter
              value={(table.getColumn('amountMinorUnits')?.getFilterValue() as AmountFilter | undefined) ?? {}}
              onChange={x => table.getColumn('amountMinorUnits')?.setFilterValue(x.min || x.max ? x : undefined)}
            />
          </FilterField>
        </Card>
      )}
      <div className="overflow-hidden rounded-lg border border-zinc-200 bg-white">
        <Table>
          <TableHeader className="bg-zinc-100 text-xs uppercase text-zinc-500">
            {table.getHeaderGroups().map(x => (
              <TableRow key={x.id}>{x.headers.map(y => <TableHead className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.header, y.getContext())}</TableHead>)}</TableRow>
            ))}
          </TableHeader>
          <TableBody className="divide-y divide-zinc-200">
            {table.getRowModel().rows.map(x => (
              <TableRow key={x.id}>{x.getVisibleCells().map(y => <TableCell className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.cell, y.getContext())}</TableCell>)}</TableRow>
            ))}
            {!transactions.isLoading && table.getRowModel().rows.length === 0 && <TableRow><TableCell className="px-4 py-8 text-muted-foreground" colSpan={5}>{hasFilters ? 'No transactions match the current filters.' : 'No transactions imported yet.'}</TableCell></TableRow>}
          </TableBody>
        </Table>
      </div>
    </section>
  )
}

function FilterField({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <label className={`grid gap-1.5 ${className ?? ''}`}>
      <span className="text-sm font-medium text-foreground">{label}</span>
      {children}
    </label>
  )
}

function DateRangeFilter({ value, onChange }: { value: DateFilter; onChange: (value: DateFilter) => void }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, from: x.target.value })}
        type="date"
        value={value.from ?? ''}
      />
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, to: x.target.value })}
        type="date"
        value={value.to ?? ''}
      />
    </div>
  )
}

function AmountRangeFilter({ value, onChange }: { value: AmountFilter; onChange: (value: AmountFilter) => void }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, min: x.target.value })}
        placeholder="Min"
        type="number"
        value={value.min ?? ''}
      />
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, max: x.target.value })}
        placeholder="Max"
        type="number"
        value={value.max ?? ''}
      />
    </div>
  )
}
