import { Outlet } from '@tanstack/react-router'
import { Activity, Banknote, CircleDollarSign, Landmark, ReceiptText, RefreshCcw, Search, Settings as SettingsIcon, Target, WalletCards } from 'lucide-react'
import { ThemeToggle } from '../components/ThemeToggle'
import { NavLink } from './NavLink'

export function Shell() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <aside className="fixed inset-y-0 left-0 hidden w-64 border-r border-border bg-card lg:block">
        <div className="flex h-16 items-center justify-between gap-2 border-b border-border px-6">
          <div className="flex min-w-0 items-center gap-2">
            <CircleDollarSign className="size-6 shrink-0 text-primary" />
            <span className="truncate font-semibold">Finance</span>
          </div>
          <ThemeToggle />
        </div>
        <nav className="space-y-1 p-3">
          <NavLink to="/" icon={<Activity className="size-4" />} label="Overview" />
          <NavLink to="/accounts" icon={<Banknote className="size-4" />} label="Accounts" />
          <NavLink to="/transactions" icon={<Search className="size-4" />} label="Transactions" />
          <NavLink to="/budgets" icon={<Target className="size-4" />} label="Budgets" />
          <NavLink to="/pay-breakdowns" icon={<WalletCards className="size-4" />} label="Breakdowns" />
          <NavLink to="/home-loans" icon={<Landmark className="size-4" />} label="Home Loans" />
          <NavLink to="/subscriptions" icon={<ReceiptText className="size-4" />} label="Subscriptions" />
          <NavLink to="/imports" icon={<RefreshCcw className="size-4" />} label="Imports" />
          <NavLink to="/settings" icon={<SettingsIcon className="size-4" />} label="Settings" />
        </nav>
      </aside>
      <header className="border-b border-border bg-card lg:hidden">
        <div className="flex h-14 items-center justify-between gap-2 px-4">
          <div className="flex min-w-0 items-center gap-2">
            <CircleDollarSign className="size-6 shrink-0 text-primary" />
            <span className="truncate font-semibold">Finance</span>
          </div>
          <ThemeToggle />
        </div>
        <nav className="flex gap-1 overflow-x-auto px-2 pb-2">
          <NavLink to="/" icon={<Activity className="size-4" />} label="Overview" />
          <NavLink to="/accounts" icon={<Banknote className="size-4" />} label="Accounts" />
          <NavLink to="/transactions" icon={<Search className="size-4" />} label="Transactions" />
          <NavLink to="/budgets" icon={<Target className="size-4" />} label="Budgets" />
          <NavLink to="/pay-breakdowns" icon={<WalletCards className="size-4" />} label="Breakdowns" />
          <NavLink to="/home-loans" icon={<Landmark className="size-4" />} label="Home Loans" />
          <NavLink to="/subscriptions" icon={<ReceiptText className="size-4" />} label="Subscriptions" />
          <NavLink to="/imports" icon={<RefreshCcw className="size-4" />} label="Imports" />
          <NavLink to="/settings" icon={<SettingsIcon className="size-4" />} label="Settings" />
        </nav>
      </header>
      <main className="lg:pl-64">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
