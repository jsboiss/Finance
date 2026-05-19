import { Check, CheckCircle2, DatabaseZap, Loader2, Pencil, Trash2, X } from 'lucide-react'
import { useState } from 'react'
import { currency } from '../lib/format'
import { cn } from '../lib/utils'
import type { Account } from '../lib/types'

export function AccountsList({
  accounts,
  isLoading,
  isSaving,
  isOperating,
  syncingAccountId,
  lastSyncedAccountId,
  onUpdate,
  onSync,
  onClear
}: {
  accounts: Account[]
  isLoading: boolean
  isSaving: boolean
  isOperating: boolean
  syncingAccountId: string | null
  lastSyncedAccountId: string | null
  onUpdate: (accountId: string, customName: string, accountType: Account['accountType']) => void
  onSync: (accountId: string) => void
  onClear: (accountId: string, displayName: string) => void
}) {
  const [editingAccountId, setEditingAccountId] = useState<string | null>(null)
  const [customName, setCustomName] = useState('')
  const [accountType, setAccountType] = useState<Account['accountType']>('Everyday')

  function startEditing(account: Account) {
    setEditingAccountId(account.id)
    setCustomName(account.customName)
    setAccountType(account.accountType)
  }

  function stopEditing() {
    setEditingAccountId(null)
    setCustomName('')
    setAccountType('Everyday')
  }

  function saveName(accountId: string) {
    onUpdate(accountId, customName, accountType)
    stopEditing()
  }

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-card">
      {accounts.map(account => (
        <div
          className={cn(
            'grid gap-3 border-b border-border p-4 last:border-b-0 md:grid-cols-[1fr_auto] md:items-center',
            account.id === syncingAccountId && 'bg-primary/5'
          )}
          key={account.id}
        >
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
                <select
                  className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                  onChange={x => setAccountType(x.target.value as Account['accountType'])}
                  value={accountType}
                >
                  {accountTypes.map(x => <option key={x.value} value={x.value}>{x.label}</option>)}
                </select>
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
                  <p className="mt-1 text-xs text-muted-foreground">
                    {account.customName ? `${account.name} - ` : ''}{formatAccountType(account.accountType)}
                    {!account.includeInEverydayAnalytics ? ' - excluded from everyday analytics' : ''}
                  </p>
                  {account.id === syncingAccountId && <p className="mt-1 text-xs font-medium text-foreground">Syncing now</p>}
                  {account.id === lastSyncedAccountId && account.id !== syncingAccountId && <p className="mt-1 text-xs font-medium text-emerald-600 dark:text-emerald-400">Sync complete</p>}
                </div>
                <button aria-label={`Edit custom name for ${account.displayName}`} className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border border-transparent text-muted-foreground hover:border-border hover:bg-muted hover:text-foreground" onClick={() => startEditing(account)} type="button">
                  <Pencil className="size-4" />
                </button>
              </div>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-2 md:justify-end">
            <p className="mr-2 text-xl font-semibold">{currency(account.currentBalanceMinorUnits, account.currency)}</p>
            <button aria-label={`Sync ${account.displayName}`} className="inline-flex size-9 items-center justify-center rounded-md border border-input hover:bg-muted disabled:opacity-50" disabled={isOperating} onClick={() => onSync(account.id)} type="button">
              {account.id === syncingAccountId ? <Loader2 className="size-4 animate-spin" /> : account.id === lastSyncedAccountId ? <CheckCircle2 className="size-4 text-emerald-600 dark:text-emerald-400" /> : <DatabaseZap className="size-4" />}
            </button>
            <button aria-label={`Clear imported data for ${account.displayName}`} className="inline-flex size-9 items-center justify-center rounded-md border border-input text-destructive hover:bg-destructive/10 disabled:opacity-50" disabled={isOperating} onClick={() => onClear(account.id, account.displayName)} type="button">
              <Trash2 className="size-4" />
            </button>
          </div>
        </div>
      ))}
      {!isLoading && accounts.length === 0 && <p className="text-sm text-muted-foreground">No accounts imported yet.</p>}
    </div>
  )
}

const accountTypes: Array<{ value: Account['accountType']; label: string }> = [
  { value: 'Everyday', label: 'Everyday' },
  { value: 'Savings', label: 'Savings' },
  { value: 'CreditCard', label: 'Credit card' },
  { value: 'HomeLoan', label: 'Home loan' },
  { value: 'Offset', label: 'Offset' },
  { value: 'Other', label: 'Other' }
]

function formatAccountType(accountType: Account['accountType']) {
  return accountTypes.find(x => x.value === accountType)?.label ?? accountType
}
