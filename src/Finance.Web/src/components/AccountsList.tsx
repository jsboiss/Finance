import { currency } from '../lib/format'
import type { Account } from '../lib/types'

export function AccountsList({ accounts, isLoading }: { accounts: Account[]; isLoading: boolean }) {
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
