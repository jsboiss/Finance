import { Check, Pencil, X } from 'lucide-react'
import { useState } from 'react'
import { currency } from '../lib/format'
import type { Account } from '../lib/types'
import { Card, CardContent, CardHeader, CardTitle } from './ui/card'

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
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {accounts.map(account => (
        <Card key={account.id}>
          <CardHeader className="gap-3">
            <p className="text-sm text-muted-foreground">{account.institutionName}</p>
            {editingAccountId === account.id ? (
              <div className="grid gap-2">
                <input
                  autoFocus
                  className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
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
                <div className="flex items-center gap-2">
                  <button aria-label={`Save custom name for ${account.displayName}`} className="inline-flex size-8 items-center justify-center rounded-md border border-input hover:bg-muted disabled:opacity-50" disabled={isSaving} onClick={() => saveName(account.id)} type="button">
                    <Check className="size-4" />
                  </button>
                  <button aria-label="Cancel custom name edit" className="inline-flex size-8 items-center justify-center rounded-md border border-input hover:bg-muted" onClick={stopEditing} type="button">
                    <X className="size-4" />
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <CardTitle className="break-words">{account.displayName}</CardTitle>
                  {account.customName && <p className="mt-1 text-xs text-muted-foreground">{account.name}</p>}
                </div>
                <button aria-label={`Edit custom name for ${account.displayName}`} className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border border-transparent text-muted-foreground hover:border-border hover:bg-muted hover:text-foreground" onClick={() => startEditing(account)} type="button">
                  <Pencil className="size-4" />
                </button>
              </div>
            )}
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">{currency(account.currentBalanceMinorUnits, account.currency)}</p>
          </CardContent>
        </Card>
      ))}
      {!isLoading && accounts.length === 0 && <p className="text-sm text-muted-foreground">No accounts imported yet.</p>}
    </div>
  )
}
