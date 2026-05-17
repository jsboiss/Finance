import { Check, Pencil, X } from 'lucide-react'
import { useState } from 'react'
import { currency } from '../lib/format'
import type { Account } from '../lib/types'

export function AccountsList({ accounts, isLoading, isSaving, onRename }: { accounts: Account[]; isLoading: boolean; isSaving: boolean; onRename: (accountId: string, customName: string) => void }) {
  const [editingAccountId, setEditingAccountId] = useState<string | null>(null)
  const [customName, setCustomName] = useState('')

  function startEditing(account: Account) {
    setEditingAccountId(account.id)
    setCustomName(account.customName)
  }

  function stopEditing() {
    setEditingAccountId(null)
    setCustomName('')
  }

  function saveName(accountId: string) {
    onRename(accountId, customName)
    stopEditing()
  }

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-card">
      {accounts.map(account => (
        <div className="grid gap-3 border-b border-border p-4 last:border-b-0 md:grid-cols-[1fr_auto] md:items-center" key={account.id}>
          <div className="min-w-0">
            <p className="text-sm text-muted-foreground">{account.institutionName}</p>
            {editingAccountId === account.id ? (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <input
                  autoFocus
                  className="h-9 min-w-0 flex-1 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30 sm:max-w-md"
                  onChange={x => setCustomName(x.target.value)}
                  onKeyDown={x => {
                    if (x.key === 'Enter') {
                      saveName(account.id)
                    }

                    if (x.key === 'Escape') {
                      stopEditing()
                    }
                  }}
                  placeholder={account.name}
                  value={customName}
                />
                <button aria-label={`Save custom name for ${account.displayName}`} className="inline-flex size-9 items-center justify-center rounded-md border border-input hover:bg-muted disabled:opacity-50" disabled={isSaving} onClick={() => saveName(account.id)} type="button">
                  <Check className="size-4" />
                </button>
                <button aria-label="Cancel custom name edit" className="inline-flex size-9 items-center justify-center rounded-md border border-input hover:bg-muted" onClick={stopEditing} type="button">
                  <X className="size-4" />
                </button>
              </div>
            ) : (
              <div className="mt-1 flex min-w-0 items-center gap-2">
                <div className="min-w-0">
                  <p className="break-words text-base font-medium leading-snug">{account.displayName}</p>
                  {account.customName && <p className="mt-1 text-xs text-muted-foreground">{account.name}</p>}
                </div>
                <button aria-label={`Edit custom name for ${account.displayName}`} className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border border-transparent text-muted-foreground hover:border-border hover:bg-muted hover:text-foreground" onClick={() => startEditing(account)} type="button">
                  <Pencil className="size-4" />
                </button>
              </div>
            )}
          </div>
          <p className="text-xl font-semibold md:text-right">{currency(account.currentBalanceMinorUnits, account.currency)}</p>
        </div>
      ))}
      {!isLoading && accounts.length === 0 && <p className="text-sm text-muted-foreground">No accounts imported yet.</p>}
    </div>
  )
}
