import { Outlet } from '@tanstack/react-router'
import { Activity, Banknote, CircleDollarSign, RefreshCcw, Search } from 'lucide-react'
import { NavLink } from './NavLink'

export function Shell() {
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
