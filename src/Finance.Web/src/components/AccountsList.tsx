import { currency } from '../lib/format'
import type { Account } from '../lib/types'
import { Card, CardContent, CardHeader, CardTitle } from './ui/card'

export function AccountsList({ accounts, isLoading }: { accounts: Account[]; isLoading: boolean }) {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {accounts.map(account => (
        <Card key={account.id}>
          <CardHeader>
            <p className="text-sm text-muted-foreground">{account.institutionName}</p>
            <CardTitle>{account.displayName}</CardTitle>
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
