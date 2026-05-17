import { useQuery } from '@tanstack/react-query'
import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table'
import { Header } from '../components/Header'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Transaction } from '../lib/types'

export function Transactions() {
  const transactions = useQuery({ queryKey: ['transactions'], queryFn: () => api<Transaction[]>('/api/transactions?pageSize=100') })
  const helper = createColumnHelper<Transaction>()
  const table = useReactTable({
    data: transactions.data ?? [],
    columns: [
      helper.accessor('postedDate', { header: 'Date' }),
      helper.accessor('description', { header: 'Description' }),
      helper.accessor('amountMinorUnits', { header: 'Amount', cell: x => currency(x.getValue(), x.row.original.currency) })
    ],
    getCoreRowModel: getCoreRowModel()
  })

  return (
    <section className="space-y-6">
      <Header title="Transactions" subtitle="Posted transactions only, ready for filtering and reconciliation checks." />
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
            {!transactions.isLoading && table.getRowModel().rows.length === 0 && <TableRow><TableCell className="px-4 py-8 text-muted-foreground" colSpan={3}>No transactions imported yet.</TableCell></TableRow>}
          </TableBody>
        </Table>
      </div>
    </section>
  )
}
