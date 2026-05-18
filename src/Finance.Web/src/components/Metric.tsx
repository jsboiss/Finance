import type React from 'react'
import { Card, CardAction, CardContent, CardHeader } from './ui/card'

export function Metric({ action, label, value }: { action?: React.ReactNode; label: string; value: string }) {
  return (
    <Card>
      <CardHeader>
        <p className="text-sm text-muted-foreground">{label}</p>
        {action && <CardAction>{action}</CardAction>}
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-semibold">{value}</p>
      </CardContent>
    </Card>
  )
}
