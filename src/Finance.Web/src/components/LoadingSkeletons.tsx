import { Card, CardContent, CardHeader } from './ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './ui/table'

function SkeletonBlock({ className }: { className: string }) {
  return <div className={`animate-pulse rounded bg-muted ${className}`} />
}

export function MetricGridLoading({ count = 4 }: { count?: number }) {
  return (
    <div className="grid gap-3 md:grid-cols-4">
      {Array.from({ length: count }, (_, x) => (
        <Card className="p-4" key={x}>
          <SkeletonBlock className="h-4 w-24" />
          <SkeletonBlock className="mt-3 h-8 w-32" />
        </Card>
      ))}
    </div>
  )
}

export function FormCardLoading({ fields = 4 }: { fields?: number }) {
  return (
    <Card>
      <CardHeader>
        <SkeletonBlock className="h-5 w-36" />
        <SkeletonBlock className="mt-2 h-4 w-64 max-w-full" />
      </CardHeader>
      <CardContent>
        <div className="grid gap-3 lg:grid-cols-4">
          {Array.from({ length: fields }, (_, x) => <SkeletonBlock className="h-9" key={x} />)}
        </div>
      </CardContent>
    </Card>
  )
}

export function CardGridLoading({ count = 2 }: { count?: number }) {
  return (
    <div className="grid gap-4 xl:grid-cols-2">
      {Array.from({ length: count }, (_, x) => (
        <Card key={x}>
          <CardHeader>
            <SkeletonBlock className="h-5 w-40" />
            <SkeletonBlock className="mt-2 h-4 w-56 max-w-full" />
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-3 sm:grid-cols-3">
              {[0, 1, 2].map(y => <SkeletonBlock className="h-20" key={y} />)}
            </div>
            <div className="space-y-3">
              {[0, 1, 2].map(y => (
                <div className="rounded-md border border-border p-3" key={y}>
                  <SkeletonBlock className="h-4 w-1/2" />
                  <SkeletonBlock className="mt-3 h-2" />
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

export function ListLoading({ count = 4 }: { count?: number }) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-card">
      {Array.from({ length: count }, (_, x) => (
        <div className="grid gap-3 border-b border-border p-4 last:border-b-0 md:grid-cols-[1fr_auto] md:items-center" key={x}>
          <div>
            <SkeletonBlock className="h-4 w-32" />
            <SkeletonBlock className="mt-3 h-5 w-56 max-w-full" />
            <SkeletonBlock className="mt-2 h-3 w-44 max-w-full" />
          </div>
          <div className="flex items-center gap-2 md:justify-end">
            <SkeletonBlock className="h-7 w-28" />
            <SkeletonBlock className="size-9" />
            <SkeletonBlock className="size-9" />
          </div>
        </div>
      ))}
    </div>
  )
}

export function TableLoading({ columns, rows = 6 }: { columns: number; rows?: number }) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-card">
      <Table>
        <TableHeader className="bg-muted text-xs uppercase text-muted-foreground">
          <TableRow>
            {Array.from({ length: columns }, (_, x) => <TableHead className="px-4 py-3" key={x}><SkeletonBlock className="h-3 w-20" /></TableHead>)}
          </TableRow>
        </TableHeader>
        <TableBody className="divide-y divide-border">
          {Array.from({ length: rows }, (_, x) => (
            <TableRow key={x}>
              {Array.from({ length: columns }, (_, y) => (
                <TableCell className="px-4 py-3" key={y}>
                  <SkeletonBlock className={y === 0 ? 'h-5 w-40 max-w-full' : 'h-5 w-24 max-w-full'} />
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
