import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query'
import { createRootRoute, createRoute, createRouter, Link, Outlet, RouterProvider } from '@tanstack/react-router'
import { getCoreRowModel, useReactTable, flexRender, createColumnHelper } from '@tanstack/react-table'
import { Activity, Banknote, CircleDollarSign, RefreshCcw, Search } from 'lucide-react'
import './index.css'

type Account = { id: string; name: string; institutionName: string; currency: string; currentBalanceMinorUnits: number | null }
type Balance = { accountId: string; currentMinorUnits: number | null; currency: string; asOf: string }
type Transaction = { id: string; accountId: string; description: string; amountMinorUnits: number; currency: string; postedDate: string }
type ImportRun = { id: string; source: string; status: string; startedAt: string; completedAt?: string; importedCount: number; error?: string }

const queryClient = new QueryClient()
const currency = (amount: number | null | undefined, code: string) => amount == null ? 'Unavailable' : new Intl.NumberFormat('en-AU', { style: 'currency', currency: code }).format(amount / 100)

async function api<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`)
  }

  return response.json()
}

function Shell() {
  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-950">
      <aside className="fixed inset-y-0 left-0 hidden w-64 border-r border-zinc-200 bg-white lg:block">
        <div className="flex h-16 items-center gap-2 border-b border-zinc-200 px-6">
          <CircleDollarSign className="size-6 text-emerald-700" />
          <span className="font-semibold">Finance</span>
        </div>
        <nav className="space-y-1 p-3">
          <NavLink to="/" icon={<Activity className="size-4" />} label="Overview" />
          <NavLink to="/accounts" icon={<Banknote className="size-4" />} label="Accounts" />
          <NavLink to="/transactions" icon={<Search className="size-4" />} label="Transactions" />
          <NavLink to="/imports" icon={<RefreshCcw className="size-4" />} label="Imports" />
        </nav>
      </aside>
      <main className="lg:pl-64">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </div>
      </main>
    </div>
  )
}

function NavLink({ to, icon, label }: { to: string; icon: React.ReactNode; label: string }) {
  return (
    <Link to={to} className="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-zinc-700 hover:bg-zinc-100 [&.active]:bg-emerald-50 [&.active]:text-emerald-800">
      {icon}
      {label}
    </Link>
  )
}

function Overview() {
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const balances = useQuery({ queryKey: ['balances'], queryFn: () => api<Balance[]>('/api/balances') })
  const availableBalances = accounts.data?.filter(account => account.currentBalanceMinorUnits != null) ?? []
  const total = availableBalances.length > 0 ? availableBalances.reduce((sum, account) => sum + (account.currentBalanceMinorUnits ?? 0), 0) : null
  return (
    <section className="space-y-6">
      <Header title="Overview" subtitle="Balances, posted activity, and ingestion health." />
      <div className="grid gap-4 md:grid-cols-3">
        <Metric label="Total balance" value={currency(total, 'AUD')} />
        <Metric label="Accounts" value={`${accounts.data?.length ?? 0}`} />
        <Metric label="Balance snapshots" value={`${balances.data?.length ?? 0}`} />
      </div>
      <AccountsList accounts={accounts.data ?? []} isLoading={accounts.isLoading} />
    </section>
  )
}

function Accounts() {
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  return (
    <section className="space-y-6">
      <Header title="Accounts" subtitle="Connected Redbark accounts and current balances." />
      <AccountsList accounts={accounts.data ?? []} isLoading={accounts.isLoading} />
    </section>
  )
}

function Transactions() {
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
            {table.getHeaderGroups().map(group => (
              <tr key={group.id}>{group.headers.map(header => <th className="px-4 py-3" key={header.id}>{flexRender(header.column.columnDef.header, header.getContext())}</th>)}</tr>
            ))}
          </thead>
          <tbody className="divide-y divide-zinc-200">
            {table.getRowModel().rows.map(row => (
              <tr key={row.id}>{row.getVisibleCells().map(cell => <td className="px-4 py-3" key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>)}</tr>
            ))}
            {!transactions.isLoading && table.getRowModel().rows.length === 0 && <tr><td className="px-4 py-8 text-zinc-500" colSpan={3}>No transactions imported yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function Imports() {
  const imports = useQuery({ queryKey: ['imports'], queryFn: () => api<ImportRun[]>('/api/imports') })
  return (
    <section className="space-y-6">
      <Header title="Imports" subtitle="Backfill, webhook, and reconciliation run status." />
      <div className="grid gap-3">
        {(imports.data ?? []).map(run => (
          <div className="rounded-lg border border-zinc-200 bg-white p-4" key={run.id}>
            <div className="flex items-center justify-between gap-4">
              <div>
                <p className="font-medium">{run.source}</p>
                <p className="text-sm text-zinc-500">{new Date(run.startedAt).toLocaleString()}</p>
              </div>
              <span className="rounded-full bg-zinc-100 px-3 py-1 text-xs font-medium">{run.status}</span>
            </div>
            {run.error && <p className="mt-3 text-sm text-red-700">{run.error}</p>}
          </div>
        ))}
        {!imports.isLoading && imports.data?.length === 0 && <p className="text-sm text-zinc-500">No import runs yet.</p>}
      </div>
    </section>
  )
}

function AccountsList({ accounts, isLoading }: { accounts: Account[]; isLoading: boolean }) {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {accounts.map(account => (
        <div className="rounded-lg border border-zinc-200 bg-white p-4" key={account.id}>
          <p className="text-sm text-zinc-500">{account.institutionName}</p>
          <h2 className="mt-1 font-semibold">{account.name}</h2>
          <p className="mt-4 text-2xl font-semibold">{currency(account.currentBalanceMinorUnits, account.currency)}</p>
        </div>
      ))}
      {!isLoading && accounts.length === 0 && <p className="text-sm text-zinc-500">No accounts imported yet.</p>}
    </div>
  )
}

function Header({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <header>
      <h1 className="text-2xl font-semibold tracking-normal">{title}</h1>
      <p className="mt-1 text-sm text-zinc-600">{subtitle}</p>
    </header>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-white p-4">
      <p className="text-sm text-zinc-500">{label}</p>
      <p className="mt-2 text-2xl font-semibold">{value}</p>
    </div>
  )
}

const rootRoute = createRootRoute({ component: Shell })
const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Overview })
const accountsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/accounts', component: Accounts })
const transactionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/transactions', component: Transactions })
const importsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/imports', component: Imports })
const router = createRouter({ routeTree: rootRoute.addChildren([indexRoute, accountsRoute, transactionsRoute, importsRoute]) })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>
)
