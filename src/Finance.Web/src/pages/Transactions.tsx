import { useQuery } from '@tanstack/react-query'
import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table'
import { Header } from '../components/Header'
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
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-100 text-xs uppercase text-zinc-500">
            {table.getHeaderGroups().map(x => (
              <tr key={x.id}>{x.headers.map(y => <th className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.header, y.getContext())}</th>)}</tr>
            ))}
          </thead>
          <tbody className="divide-y divide-zinc-200">
            {table.getRowModel().rows.map(x => (
              <tr key={x.id}>{x.getVisibleCells().map(y => <td className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.cell, y.getContext())}</td>)}</tr>
            ))}
            {!transactions.isLoading && table.getRowModel().rows.length === 0 && <tr><td className="px-4 py-8 text-zinc-500" colSpan={3}>No transactions imported yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}
