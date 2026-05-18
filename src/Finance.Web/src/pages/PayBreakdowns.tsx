import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, Edit3, Plus, Trash2, WalletCards } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Header } from '../components/Header'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, PayBreakdownProfile } from '../lib/types'

type FormState = {
  id?: string
  name: string
  mainAccountId: string
  savingsAccountId: string
  fortnightlyPay: string
}

const emptyForm: FormState = {
  name: '',
  mainAccountId: '',
  savingsAccountId: '',
  fortnightlyPay: ''
}

const categoryColors: Record<string, string> = {
  personal: 'oklch(0.66 0.19 27)',
  internal: 'oklch(0.58 0.12 260)',
  savings: 'oklch(0.62 0.14 160)'
}

export function PayBreakdowns() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<FormState>(emptyForm)
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const profiles = useQuery({
    queryKey: ['pay-breakdowns'],
    queryFn: () => api<PayBreakdownProfile[]>('/api/pay-breakdowns'),
    placeholderData: keepPreviousData
  })
  const canCreateMore = (profiles.data?.length ?? 0) < 2
  const isEditing = !!form.id
  const selectedMainAccount = useMemo(() => accounts.data?.find(x => x.id === form.mainAccountId), [accounts.data, form.mainAccountId])
  const saveProfile = useMutation({
    mutationFn: () => {
      const body = JSON.stringify({
        name: form.name,
        mainAccountId: form.mainAccountId,
        savingsAccountId: form.savingsAccountId || null,
        fortnightlyPayMinorUnits: dollarsToMinorUnits(form.fortnightlyPay),
        currency: selectedMainAccount?.currency ?? 'AUD'
      })

      return api<PayBreakdownProfile>(form.id ? `/api/pay-breakdowns/${form.id}` : '/api/pay-breakdowns', {
        method: form.id ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body
      })
    },
    onSuccess: profile => {
      setForm(emptyForm)
      queryClient.setQueryData<PayBreakdownProfile[]>(['pay-breakdowns'], x => {
        const profiles = x ?? []
        return profiles.some(y => y.id === profile.id)
          ? profiles.map(y => y.id === profile.id ? profile : y)
          : [...profiles, profile]
      })
    }
  })
  const deleteProfile = useMutation({
    mutationFn: (profileId: string) => api<void>(`/api/pay-breakdowns/${profileId}`, { method: 'DELETE' }),
    onMutate: async profileId => {
      await queryClient.cancelQueries({ queryKey: ['pay-breakdowns'] })
      const previousProfiles = queryClient.getQueryData<PayBreakdownProfile[]>(['pay-breakdowns'])
      queryClient.setQueryData<PayBreakdownProfile[]>(['pay-breakdowns'], x => (x ?? []).filter(y => y.id !== profileId))
      return { previousProfiles }
    },
    onError: (_error, _profileId, context) => {
      queryClient.setQueryData(['pay-breakdowns'], context?.previousProfiles)
    }
  })

  function editProfile(profile: PayBreakdownProfile) {
    setForm({
      id: profile.id,
      name: profile.name,
      mainAccountId: profile.mainAccount.id,
      savingsAccountId: profile.savingsAccount?.id ?? '',
      fortnightlyPay: minorUnitsToDollars(profile.fortnightlyPayMinorUnits)
    })
  }

  function removeProfile(profile: PayBreakdownProfile) {
    if (window.confirm(`Delete ${profile.name}'s pay breakdown?`)) {
      deleteProfile.mutate(profile.id)
    }
  }

  return (
    <section className="space-y-6">
      <Header title="Pay breakdowns" subtitle="Compare fortnightly pay against personal spending, internal expenses, and savings transfers." />
      {(canCreateMore || isEditing) && (
        <Card>
          <CardHeader>
            <CardTitle>{isEditing ? 'Edit profile' : 'New profile'}</CardTitle>
            <CardDescription>{isEditing ? 'Update the selected accounts and fortnightly pay.' : 'Create up to two profiles for separate pay breakdowns.'}</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_minmax(0,0.7fr)_auto]">
              <input
                className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                onChange={x => setForm(y => ({ ...y, name: x.target.value }))}
                placeholder="Name"
                value={form.name}
              />
              <select
                className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                onChange={x => setForm(y => ({ ...y, mainAccountId: x.target.value, savingsAccountId: y.savingsAccountId === x.target.value ? '' : y.savingsAccountId }))}
                value={form.mainAccountId}
              >
                <option value="">Main account</option>
                {(accounts.data ?? []).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
              </select>
              <select
                className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                onChange={x => setForm(y => ({ ...y, savingsAccountId: x.target.value }))}
                value={form.savingsAccountId}
              >
                <option value="">No savings account</option>
                {(accounts.data ?? []).filter(x => x.id !== form.mainAccountId).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
              </select>
              <input
                className="h-9 rounded-md border border-input bg-background px-3 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
                inputMode="decimal"
                onChange={x => setForm(y => ({ ...y, fortnightlyPay: x.target.value }))}
                placeholder="Fortnightly pay"
                value={form.fortnightlyPay}
              />
              <div className="flex gap-2">
                <Button disabled={saveProfile.isPending || !form.name.trim() || !form.mainAccountId} onClick={() => saveProfile.mutate()}>
                  <Plus data-icon="inline-start" />
                  {isEditing ? 'Save' : 'Add'}
                </Button>
                {isEditing && <Button onClick={() => setForm(emptyForm)} variant="outline">Cancel</Button>}
              </div>
            </div>
            {saveProfile.error && <p className="mt-3 text-sm text-destructive">Could not save this breakdown. Check that the profile name is unique and both accounts are valid.</p>}
          </CardContent>
        </Card>
      )}
      {!canCreateMore && !isEditing && (
        <Card className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">Two pay breakdown profiles are configured.</p>
            <Badge variant="secondary">Limit reached</Badge>
          </div>
        </Card>
      )}
      {profiles.isLoading && <PayBreakdownLoading />}
      {!profiles.isLoading && (
        <div className={profiles.isFetching ? 'grid gap-4 opacity-70 transition-opacity xl:grid-cols-2' : 'grid gap-4 transition-opacity xl:grid-cols-2'}>
          {(profiles.data ?? []).map(x => <PayBreakdownCard key={x.id} profile={x} onDelete={removeProfile} onEdit={editProfile} />)}
        </div>
      )}
      {!profiles.isLoading && profiles.data?.length === 0 && (
        <Card className="p-8 text-center">
          <WalletCards className="mx-auto size-8 text-muted-foreground" />
          <p className="mt-3 font-medium">No pay breakdowns yet</p>
          <p className="mt-1 text-sm text-muted-foreground">Add one for yourself, then add a second one for your partner.</p>
        </Card>
      )}
    </section>
  )
}

function PayBreakdownLoading() {
  return (
    <div className="grid gap-4 xl:grid-cols-2">
      {[0, 1].map(x => (
        <Card key={x}>
          <CardHeader>
            <div className="h-5 w-40 animate-pulse rounded bg-muted" />
            <div className="mt-2 h-4 w-56 animate-pulse rounded bg-muted" />
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-3 sm:grid-cols-3">
              {[0, 1, 2].map(y => <div className="h-20 animate-pulse rounded-md bg-muted" key={y} />)}
            </div>
            <div className="space-y-3">
              {[0, 1, 2].map(y => (
                <div className="rounded-md border border-border p-3" key={y}>
                  <div className="h-4 w-1/2 animate-pulse rounded bg-muted" />
                  <div className="mt-3 h-2 animate-pulse rounded bg-muted" />
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function PayBreakdownCard({ profile, onDelete, onEdit }: { profile: PayBreakdownProfile; onDelete: (profile: PayBreakdownProfile) => void; onEdit: (profile: PayBreakdownProfile) => void }) {
  const [openCategoryKey, setOpenCategoryKey] = useState<string | null>(null)
  const totalAllocated = profile.breakdown.personalExpenseMinorUnits + profile.breakdown.internalExpenseMinorUnits + profile.breakdown.savingsTransferMinorUnits
  const max = Math.max(profile.breakdown.payMinorUnits, totalAllocated, 1)

  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{profile.name}</CardTitle>
          <CardDescription>
            {formatDate(profile.breakdown.from)} to {formatDate(profile.breakdown.to)}
            {!profile.breakdown.isPayDateMatched ? ' - no matching pay deposit found yet' : ''}
          </CardDescription>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => onEdit(profile)} size="icon" title="Edit pay breakdown" variant="outline">
            <Edit3 className="size-4" />
          </Button>
          <Button onClick={() => onDelete(profile)} size="icon" title="Delete pay breakdown" variant="destructive">
            <Trash2 className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-3">
          <BreakdownMetric label="Fortnightly pay" value={profile.breakdown.payMinorUnits} currencyCode={profile.currency} />
          <BreakdownMetric label="Allocated" value={totalAllocated} currencyCode={profile.currency} />
          <BreakdownMetric label="Remaining" value={profile.breakdown.remainingMinorUnits} currencyCode={profile.currency} />
        </div>
        <div className="space-y-3">
          {profile.breakdown.categories.map(x => (
            <div className="space-y-2" key={x.key}>
              <button
                className="grid w-full gap-1.5 rounded-md border border-transparent p-2 text-left hover:border-border hover:bg-muted/60"
                onClick={() => setOpenCategoryKey(y => y === x.key ? null : x.key)}
                type="button"
              >
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="inline-flex min-w-0 items-center gap-2 font-medium">
                    <ChevronDown className={openCategoryKey === x.key ? 'size-4 shrink-0 transition-transform' : 'size-4 shrink-0 -rotate-90 transition-transform'} />
                    <span className="truncate">{x.label}</span>
                    <span className="shrink-0 text-xs text-muted-foreground">{x.transactions.length}</span>
                  </span>
                  <span className="shrink-0 font-semibold">{currency(x.amountMinorUnits, profile.currency)}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-muted">
                  <div className="h-full rounded-full" style={{ backgroundColor: categoryColors[x.key] ?? 'oklch(0.55 0.02 250)', width: `${Math.max((x.amountMinorUnits / max) * 100, x.amountMinorUnits > 0 ? 2 : 0)}%` }} />
                </div>
              </button>
              {openCategoryKey === x.key && (
                <div className="overflow-hidden rounded-md border border-border">
                  {x.transactions.map(y => (
                    <div className="grid gap-1 border-b border-border px-3 py-2 last:border-b-0 sm:grid-cols-[1fr_auto] sm:items-center" key={y.id}>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium">{y.merchantName || y.description}</p>
                        <p className="truncate text-xs text-muted-foreground">{formatDate(y.postedDate)}{y.merchantName ? ` - ${y.description}` : ''}</p>
                      </div>
                      <p className="text-sm font-semibold sm:text-right">{currency(y.amountMinorUnits, y.currency)}</p>
                    </div>
                  ))}
                  {x.transactions.length === 0 && <p className="px-3 py-2 text-sm text-muted-foreground">No transactions in this category.</p>}
                </div>
              )}
            </div>
          ))}
        </div>
        <div className="grid gap-2 rounded-md border border-border bg-muted p-3 text-sm">
          <p><span className="text-muted-foreground">Main:</span> {profile.mainAccount.displayName}</p>
          <p><span className="text-muted-foreground">Savings:</span> {profile.savingsAccount?.displayName ?? 'Not set'}</p>
        </div>
      </CardContent>
    </Card>
  )
}

function BreakdownMetric({ label, value, currencyCode }: { label: string; value: number; currencyCode: string }) {
  return (
    <div className="rounded-md border border-border bg-muted p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={value < 0 ? 'mt-1 text-lg font-semibold text-destructive' : 'mt-1 text-lg font-semibold'}>{currency(value, currencyCode)}</p>
    </div>
  )
}

function dollarsToMinorUnits(value: string) {
  return Math.round(Number.parseFloat(value || '0') * 100)
}

function minorUnitsToDollars(value: number) {
  return (value / 100).toFixed(2)
}

function formatDate(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
}
