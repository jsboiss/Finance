import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createColumnHelper, flexRender, getCoreRowModel, getFilteredRowModel, type ColumnFiltersState, useReactTable } from '@tanstack/react-table'
import { Loader2, Plus, SlidersHorizontal, Trash2, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { Header } from '../components/Header'
import { TableLoading } from '../components/LoadingSkeletons'
import { Button } from '../components/ui/button'
import { Card } from '../components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table'
import { api } from '../lib/api'
import { currency } from '../lib/format'
import type { Account, MerchantTagRule, Transaction, TransactionTag } from '../lib/types'

type DateFilter = {
  from?: string
  to?: string
}

type AmountFilter = {
  min?: string
  max?: string
}

type CreateTagInput = {
  name: string
  color: string
}

type SetTransactionTagsInput = {
  transactionId: string
  tagIds: string[]
}

type CreateMerchantRuleInput = {
  merchantName: string
  tagId: string
}

const tagColorOptions = ['#bae6fd', '#bbf7d0', '#fde68a', '#fecdd3', '#ddd6fe', '#fed7aa', '#ccfbf1', '#e9d5ff']
const transactionColumnHelper = createColumnHelper<Transaction>()

export function Transactions() {
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [showFilters, setShowFilters] = useState(false)
  const [showTagManagement, setShowTagManagement] = useState(false)
  const [tagName, setTagName] = useState('')
  const [tagColor, setTagColor] = useState('#64748b')
  const [merchantName, setMerchantName] = useState('')
  const [merchantTagId, setMerchantTagId] = useState('')
  const queryClient = useQueryClient()
  const transactions = useQuery({
    queryKey: ['transactions'],
    queryFn: () => api<Transaction[]>('/api/transactions?pageSize=100'),
    placeholderData: keepPreviousData
  })
  const accounts = useQuery({ queryKey: ['accounts'], queryFn: () => api<Account[]>('/api/accounts') })
  const tags = useQuery({ queryKey: ['tags'], queryFn: () => api<TransactionTag[]>('/api/tags') })
  const merchantRules = useQuery({
    enabled: showTagManagement,
    queryKey: ['merchant-tags'],
    queryFn: () => api<MerchantTagRule[]>('/api/merchant-tags')
  })
  const createTag = useMutation({
    mutationFn: (input: CreateTagInput) => api<TransactionTag>('/api/tags', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input)
    }),
    onMutate: async input => {
      await queryClient.cancelQueries({ queryKey: ['tags'] })
      const previousTags = queryClient.getQueryData<TransactionTag[]>(['tags'])
      const optimisticTag = { id: `pending-${crypto.randomUUID()}`, name: input.name, color: input.color }
      queryClient.setQueryData<TransactionTag[]>(['tags'], x => [...(x ?? []), optimisticTag])
      setTagName('')
      return { optimisticTagId: optimisticTag.id, previousTags }
    },
    onError: (_error, _input, context) => {
      queryClient.setQueryData(['tags'], context?.previousTags)
    },
    onSuccess: (tag, _input, context) => {
      queryClient.setQueryData<TransactionTag[]>(['tags'], x => uniqueTagsById((x ?? []).map(y => y.id === context.optimisticTagId ? tag : y)))
    }
  })
  const deleteTag = useMutation({
    mutationFn: (tagId: string) => api<void>(`/api/tags/${tagId}`, { method: 'DELETE' }),
    onMutate: async tagId => {
      await queryClient.cancelQueries({ queryKey: ['tags'] })
      await queryClient.cancelQueries({ queryKey: ['transactions'] })
      await queryClient.cancelQueries({ queryKey: ['merchant-tags'] })
      const previousTags = queryClient.getQueryData<TransactionTag[]>(['tags'])
      const previousTransactions = queryClient.getQueryData<Transaction[]>(['transactions'])
      const previousMerchantRules = queryClient.getQueryData<MerchantTagRule[]>(['merchant-tags'])
      queryClient.setQueryData<TransactionTag[]>(['tags'], x => (x ?? []).filter(y => y.id !== tagId))
      queryClient.setQueryData<Transaction[]>(['transactions'], x => (x ?? []).map(y => ({ ...y, tags: y.tags.filter(z => z.id !== tagId) })))
      queryClient.setQueryData<MerchantTagRule[]>(['merchant-tags'], x => (x ?? []).filter(y => y.tag.id !== tagId))
      return { previousTags, previousTransactions, previousMerchantRules }
    },
    onError: (_error, _tagId, context) => {
      queryClient.setQueryData(['tags'], context?.previousTags)
      queryClient.setQueryData(['transactions'], context?.previousTransactions)
      queryClient.setQueryData(['merchant-tags'], context?.previousMerchantRules)
    }
  })
  const updateTransactionTags = useMutation({
    mutationFn: (input: SetTransactionTagsInput) => api<TransactionTag[]>(`/api/transactions/${input.transactionId}/tags`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tagIds: input.tagIds })
    }),
    onMutate: async input => {
      await queryClient.cancelQueries({ queryKey: ['transactions'] })
      const previousTransactions = queryClient.getQueryData<Transaction[]>(['transactions'])
      const allTags = queryClient.getQueryData<TransactionTag[]>(['tags']) ?? []
      const nextTags = allTags.filter(x => input.tagIds.includes(x.id))
      queryClient.setQueryData<Transaction[]>(['transactions'], x => (x ?? []).map(y => y.id === input.transactionId ? { ...y, tags: nextTags } : y))
      return { previousTransactions }
    },
    onError: (_error, _input, context) => {
      queryClient.setQueryData(['transactions'], context?.previousTransactions)
    },
    onSuccess: (nextTags, input) => {
      queryClient.setQueryData<Transaction[]>(['transactions'], x => (x ?? []).map(y => y.id === input.transactionId ? { ...y, tags: nextTags } : y))
    }
  })
  const setTransactionTags = useCallback((transactionId: string, tagIds: string[]) => {
    updateTransactionTags.mutate({ transactionId, tagIds })
  }, [updateTransactionTags])
  const createMerchantRule = useMutation({
    mutationFn: (input: CreateMerchantRuleInput) => api<MerchantTagRule>('/api/merchant-tags', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input)
    }),
    onMutate: async input => {
      await queryClient.cancelQueries({ queryKey: ['merchant-tags'] })
      await queryClient.cancelQueries({ queryKey: ['transactions'] })
      const previousMerchantRules = queryClient.getQueryData<MerchantTagRule[]>(['merchant-tags'])
      const previousTransactions = queryClient.getQueryData<Transaction[]>(['transactions'])
      const tag = queryClient.getQueryData<TransactionTag[]>(['tags'])?.find(x => x.id === input.tagId)
      const optimisticRuleId = `pending-${crypto.randomUUID()}`
      if (tag) {
        const merchantKey = getMerchantKey(input.merchantName)
        queryClient.setQueryData<MerchantTagRule[]>(['merchant-tags'], x => [...(x ?? []), { id: optimisticRuleId, merchantName: input.merchantName, tag }])
        queryClient.setQueryData<Transaction[]>(['transactions'], x => (x ?? []).map(y => {
          if (!y.merchantName || getMerchantKey(y.merchantName) !== merchantKey || y.tags.some(z => z.id === tag.id)) {
            return y
          }

          return { ...y, tags: [...y.tags, tag] }
        }))
      }

      setMerchantName('')
      return { optimisticRuleId, previousMerchantRules, previousTransactions }
    },
    onError: (_error, _input, context) => {
      queryClient.setQueryData(['merchant-tags'], context?.previousMerchantRules)
      queryClient.setQueryData(['transactions'], context?.previousTransactions)
    },
    onSuccess: (rule, _input, context) => {
      queryClient.setQueryData<MerchantTagRule[]>(['merchant-tags'], x => uniqueMerchantRulesById((x ?? []).map(y => y.id === context.optimisticRuleId ? rule : y)))
    }
  })
  const deleteMerchantRule = useMutation({
    mutationFn: (ruleId: string) => api<void>(`/api/merchant-tags/${ruleId}`, { method: 'DELETE' }),
    onMutate: async ruleId => {
      await queryClient.cancelQueries({ queryKey: ['merchant-tags'] })
      const previousMerchantRules = queryClient.getQueryData<MerchantTagRule[]>(['merchant-tags'])
      queryClient.setQueryData<MerchantTagRule[]>(['merchant-tags'], x => (x ?? []).filter(y => y.id !== ruleId))
      return { previousMerchantRules }
    },
    onError: (_error, _ruleId, context) => {
      queryClient.setQueryData(['merchant-tags'], context?.previousMerchantRules)
    }
  })
  const columns = useMemo(() => [
    transactionColumnHelper.accessor('postedDate', {
      header: 'Date',
      filterFn: (x, y, z: DateFilter) => {
        const value = x.getValue<string>(y)
        return (!z.from || value >= z.from) && (!z.to || value <= z.to)
      }
    }),
    transactionColumnHelper.accessor('accountId', {
      header: 'Account',
      cell: x => (
        <span
          className="inline-flex rounded-md border border-[hsl(var(--account-hue)_42%_82%)] bg-[hsl(var(--account-hue)_55%_94%)] px-2 py-1 text-xs font-medium text-[hsl(var(--account-hue)_38%_28%)] dark:border-[hsl(var(--account-hue)_28%_32%)] dark:bg-[hsl(var(--account-hue)_30%_20%)] dark:text-[hsl(var(--account-hue)_32%_78%)]"
          style={{ '--account-hue': getAccountHue(x.getValue()) } as CSSProperties}
        >
          {x.row.original.accountDisplayName}
        </span>
      ),
      filterFn: (x, y, z: string) => x.getValue<string>(y) === z
    }),
    transactionColumnHelper.accessor('description', {
      header: 'Description',
      cell: x => (
        <div className="min-w-64 whitespace-normal">
          <p>{x.getValue()}</p>
          <p className="min-h-4 text-xs text-muted-foreground">{x.row.original.merchantName}</p>
        </div>
      ),
      filterFn: 'includesString'
    }),
    transactionColumnHelper.accessor('category', {
      header: 'Category',
      filterFn: 'includesString'
    }),
    transactionColumnHelper.accessor('tags', {
      header: 'Tags',
      cell: x => (
        <TagEditor
          allTags={tags.data ?? []}
          selectedTags={x.row.original.tags}
          onChange={y => setTransactionTags(x.row.original.id, y)}
        />
      ),
      filterFn: (x, y, z: string) => x.getValue<TransactionTag[]>(y).some(a => a.name.toLowerCase().includes(z.toLowerCase()))
    }),
    transactionColumnHelper.accessor('amountMinorUnits', {
      header: 'Amount',
      cell: x => <span className={x.getValue() < 0 ? 'text-red-600 dark:text-red-400' : 'text-green-600 dark:text-green-400'}>{currency(x.getValue(), x.row.original.currency)}</span>,
      filterFn: (x, y, z: AmountFilter) => {
        const value = x.getValue<number>(y) / 100
        const min = z.min ? Number(z.min) : null
        const max = z.max ? Number(z.max) : null
        return (min == null || value >= min) && (max == null || value <= max)
      }
    })
  ], [setTransactionTags, tags.data])
  const table = useReactTable({
    data: transactions.data ?? [],
    columns,
    state: { columnFilters, columnVisibility: { category: false } },
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel()
  })
  const hasFilters = columnFilters.length > 0
  const isLoading = transactions.isLoading || transactions.isFetching

  return (
    <section className="space-y-6">
      <Header title="Transactions" subtitle="Posted transactions only, ready for filtering and reconciliation checks." />
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className={isLoading ? 'h-4 w-4 animate-spin opacity-100' : 'h-4 w-4 opacity-0'} />
          <span>Showing {table.getRowModel().rows.length} of {transactions.data?.length ?? 0} transactions</span>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => setShowTagManagement(x => !x)} size="sm" variant={showTagManagement ? 'secondary' : 'outline'}>
            <Plus data-icon="inline-start" />
            Tags
          </Button>
          <Button onClick={() => setShowFilters(x => !x)} size="sm" variant={showFilters ? 'secondary' : 'outline'}>
            <SlidersHorizontal data-icon="inline-start" />
            Filters
          </Button>
          <Button disabled={!hasFilters} onClick={() => table.resetColumnFilters()} size="sm" variant="outline">
            <X data-icon="inline-start" />
            Clear
          </Button>
        </div>
      </div>
      {showTagManagement && (
        <Card className="grid gap-6 p-4 lg:grid-cols-[1fr_1.5fr] lg:gap-8">
          <div className="space-y-3 lg:pr-8">
            <h2 className="text-sm font-semibold text-foreground">Tags</h2>
            <div className="grid gap-2 sm:grid-cols-[1fr_auto_auto]">
              <input className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setTagName(x.target.value)} placeholder="New tag" value={tagName} />
              <div className="flex h-9 items-center gap-1.5 rounded-md border border-input bg-background px-2">
                {tagColorOptions.map(x => (
                  <button
                    aria-label={`Use tag color ${x}`}
                    className={`h-5 w-5 rounded-full border border-border ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring ${tagColor.toLowerCase() === x ? 'ring-2 ring-foreground' : ''}`}
                    key={x}
                    onClick={() => setTagColor(x)}
                    style={{ backgroundColor: x }}
                    type="button"
                  />
                ))}
                <input aria-label="Custom tag color" className="h-6 w-8 rounded border-0 bg-transparent p-0" onChange={x => setTagColor(x.target.value)} type="color" value={tagColor} />
              </div>
              <Button className="h-9" disabled={!tagName.trim() || createTag.isPending} onClick={() => createTag.mutate({ name: tagName.trim(), color: tagColor })} size="sm">
                <Plus data-icon="inline-start" />
                Add
              </Button>
            </div>
            <div className="flex flex-wrap gap-2">
              {(tags.data ?? []).map(x => (
                <span className="inline-flex items-center gap-1 rounded-md border border-border bg-background px-1.5 py-1" key={x.id}>
                  <TagPill tag={x} />
                  <button aria-label={`Delete tag ${x.name}`} className="text-muted-foreground hover:text-foreground" onClick={() => deleteTag.mutate(x.id)} type="button">
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </span>
              ))}
              {!tags.isLoading && (tags.data?.length ?? 0) === 0 && <span className="text-sm text-muted-foreground">No tags yet.</span>}
            </div>
          </div>
          <div className="space-y-3 border-t border-border pt-6 lg:border-l lg:border-t-0 lg:pl-8 lg:pt-0">
            <h2 className="text-sm font-semibold text-foreground">Merchant tag rules</h2>
            <div className="grid gap-2 sm:grid-cols-[1fr_180px_auto]">
              <input className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setMerchantName(x.target.value)} placeholder="Merchant name" value={merchantName} />
              <select className="h-9 rounded-md border border-input bg-background px-2 text-sm outline-none focus:border-ring focus:ring-2 focus:ring-ring/30" onChange={x => setMerchantTagId(x.target.value)} value={merchantTagId}>
                <option value="">Select tag</option>
                {(tags.data ?? []).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
              </select>
              <Button disabled={!merchantName.trim() || !merchantTagId || createMerchantRule.isPending} onClick={() => createMerchantRule.mutate({ merchantName: merchantName.trim(), tagId: merchantTagId })} size="sm">
                <Plus data-icon="inline-start" />
                Rule
              </Button>
            </div>
            <div className="flex flex-wrap gap-2">
              {(merchantRules.data ?? []).map(x => (
                <span className="inline-flex items-center gap-2 rounded-md border border-border bg-muted px-2 py-1 text-xs" key={x.id}>
                  {x.merchantName}
                  <TagPill tag={x.tag} />
                  <button aria-label={`Delete rule for ${x.merchantName}`} className="text-muted-foreground hover:text-foreground" onClick={() => deleteMerchantRule.mutate(x.id)} type="button">
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </span>
              ))}
              {!merchantRules.isLoading && (merchantRules.data?.length ?? 0) === 0 && <span className="text-sm text-muted-foreground">No merchant rules yet.</span>}
            </div>
          </div>
        </Card>
      )}
      {showFilters && (
        <Card className="grid gap-4 p-4 md:grid-cols-2 xl:grid-cols-5">
          <FilterField className="xl:col-span-2" label="Date">
            <DateRangeFilter
              value={(table.getColumn('postedDate')?.getFilterValue() as DateFilter | undefined) ?? {}}
              onChange={x => table.getColumn('postedDate')?.setFilterValue(x.from || x.to ? x : undefined)}
            />
          </FilterField>
          <FilterField label="Account">
            <select
              className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
              disabled={accounts.isLoading}
              onChange={x => table.getColumn('accountId')?.setFilterValue(x.target.value || undefined)}
              value={(table.getColumn('accountId')?.getFilterValue() as string | undefined) ?? ''}
            >
              <option value="">All accounts</option>
              {(accounts.data ?? []).map(x => <option key={x.id} value={x.id}>{x.displayName}</option>)}
            </select>
          </FilterField>
          <FilterField label="Description">
            <DebouncedFilterInput
              onChange={x => table.getColumn('description')?.setFilterValue(x)}
              placeholder="Search descriptions"
              value={(table.getColumn('description')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField label="Category">
            <DebouncedFilterInput
              onChange={x => table.getColumn('category')?.setFilterValue(x)}
              placeholder="Search categories"
              value={(table.getColumn('category')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField label="Tags">
            <DebouncedFilterInput
              onChange={x => table.getColumn('tags')?.setFilterValue(x)}
              placeholder="Search tags"
              value={(table.getColumn('tags')?.getFilterValue() as string | undefined) ?? ''}
            />
          </FilterField>
          <FilterField className="xl:col-span-2" label="Amount">
            <AmountRangeFilter
              value={(table.getColumn('amountMinorUnits')?.getFilterValue() as AmountFilter | undefined) ?? {}}
              onChange={x => table.getColumn('amountMinorUnits')?.setFilterValue(x.min || x.max ? x : undefined)}
            />
          </FilterField>
        </Card>
      )}
      {transactions.isLoading ? (
        <TableLoading columns={5} rows={8} />
      ) : (
        <div className="overflow-hidden rounded-lg border border-border bg-card">
          <div className={isLoading ? 'h-1 bg-primary/20 opacity-100' : 'h-1 bg-primary/20 opacity-0'}>
            <div className="h-full w-1/3 animate-pulse bg-primary" />
          </div>
          <Table>
            <TableHeader className="bg-muted text-xs uppercase text-muted-foreground">
              {table.getHeaderGroups().map(x => (
                <TableRow key={x.id}>{x.headers.map(y => <TableHead className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.header, y.getContext())}</TableHead>)}</TableRow>
              ))}
            </TableHeader>
            <TableBody className="divide-y divide-border">
              {table.getRowModel().rows.map(x => (
                <TableRow key={x.id}>{x.getVisibleCells().map(y => <TableCell className="px-4 py-3" key={y.id}>{flexRender(y.column.columnDef.cell, y.getContext())}</TableCell>)}</TableRow>
              ))}
              {table.getRowModel().rows.length === 0 && <TableRow><TableCell className="px-4 py-8 text-muted-foreground" colSpan={5}>{hasFilters ? 'No transactions match the current filters.' : 'No transactions imported yet.'}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </div>
      )}
    </section>
  )
}

function FilterField({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <label className={`grid gap-1.5 ${className ?? ''}`}>
      <span className="text-sm font-medium text-foreground">{label}</span>
      {children}
    </label>
  )
}

function DebouncedFilterInput({ value, onChange, placeholder }: { value: string; onChange: (value: string) => void; placeholder: string }) {
  const [draftValue, setDraftValue] = useState(value)
  const onChangeRef = useRef(onChange)
  useEffect(() => {
    onChangeRef.current = onChange
  }, [onChange])

  useEffect(() => {
    setDraftValue(value)
  }, [value])

  useEffect(() => {
    if (draftValue === value) {
      return
    }

    const timeout = window.setTimeout(() => onChangeRef.current(draftValue), 150)
    return () => window.clearTimeout(timeout)
  }, [draftValue, value])

  return (
    <input
      className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
      onChange={x => setDraftValue(x.target.value)}
      placeholder={placeholder}
      value={draftValue}
    />
  )
}

function TagEditor({ allTags, selectedTags, onChange }: { allTags: TransactionTag[]; selectedTags: TransactionTag[]; onChange: (tagIds: string[]) => void }) {
  const [isOpen, setIsOpen] = useState(false)
  const [popupPosition, setPopupPosition] = useState<{ left: number; maxHeight: number; placement: 'above' | 'below'; top: number }>({ left: 0, maxHeight: 280, placement: 'below', top: 0 })
  const [selectedTagIds, setSelectedTagIds] = useState(() => selectedTags.map(x => x.id))
  const containerRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const selectedIds = new Set(isOpen ? selectedTagIds : selectedTags.map(x => x.id))
  const visibleSelectedTags = allTags.filter(x => selectedIds.has(x.id))
  const updatePopupPosition = useCallback(() => {
    const rect = buttonRef.current?.getBoundingClientRect()
    if (!rect) {
      return
    }

    const viewportGap = 8
    const triggerGap = 4
    const popupMinWidth = 192
    const preferredMaxHeight = 280
    const viewportHeight = window.innerHeight
    const viewportWidth = window.innerWidth
    const availableBelow = viewportHeight - rect.bottom - viewportGap - triggerGap
    const availableAbove = rect.top - viewportGap - triggerGap
    const placement = availableBelow >= preferredMaxHeight || availableBelow >= availableAbove ? 'below' : 'above'
    const availableHeight = placement === 'below' ? availableBelow : availableAbove
    const maxHeight = Math.max(0, Math.min(preferredMaxHeight, availableHeight))
    const left = Math.min(Math.max(viewportGap, rect.left), Math.max(viewportGap, viewportWidth - popupMinWidth - viewportGap))
    const top = placement === 'below' ? rect.bottom + triggerGap : rect.top - triggerGap

    setPopupPosition({ left, maxHeight, placement, top })
  }, [])

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function closeOnOutsideClick(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false)
      }
    }

    document.addEventListener('mousedown', closeOnOutsideClick)
    return () => document.removeEventListener('mousedown', closeOnOutsideClick)
  }, [isOpen, selectedTags])

  useEffect(() => {
    if (!isOpen) {
      return
    }

    updatePopupPosition()
    window.addEventListener('resize', updatePopupPosition)
    window.addEventListener('scroll', updatePopupPosition, true)
    return () => {
      window.removeEventListener('resize', updatePopupPosition)
      window.removeEventListener('scroll', updatePopupPosition, true)
    }
  }, [isOpen, updatePopupPosition])

  if (allTags.length === 0) {
    return null
  }

  return (
    <div className="relative w-fit max-w-full" ref={containerRef}>
      <button
        aria-label="Edit transaction tags"
        className="flex min-h-7 max-w-full flex-wrap items-center gap-1.5 rounded-md border border-transparent px-1.5 py-1 text-left hover:border-border hover:bg-muted"
        onClick={() => {
          updatePopupPosition()
          setSelectedTagIds(selectedTags.map(x => x.id))
          setIsOpen(true)
        }}
        ref={buttonRef}
        type="button"
      >
        {visibleSelectedTags.length > 0 ? visibleSelectedTags.map(x => <TagPill key={x.id} tag={x} />) : <Plus className="h-3.5 w-3.5 text-muted-foreground" />}
      </button>
      {isOpen && (
        <div
          className="fixed z-20 grid min-w-48 gap-1 overflow-y-auto rounded-md border border-border bg-background p-2 shadow-lg"
          style={{ left: popupPosition.left, maxHeight: popupPosition.maxHeight, top: popupPosition.top, transform: popupPosition.placement === 'above' ? 'translateY(-100%)' : undefined }}
        >
          {allTags.map(x => (
            <label className="inline-flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-xs hover:bg-muted" key={x.id}>
              <input
                checked={selectedIds.has(x.id)}
                className="h-3.5 w-3.5"
                onChange={y => {
                  const nextIds = y.target.checked ? [...selectedIds, x.id] : [...selectedIds].filter(z => z !== x.id)
                  setSelectedTagIds(nextIds)
                  onChange(nextIds)
                }}
                type="checkbox"
              />
              <TagPill tag={x} />
            </label>
          ))}
        </div>
      )}
    </div>
  )
}

function TagPill({ tag }: { tag: TransactionTag }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-medium" style={{ backgroundColor: tag.color, color: getReadableTextColor(tag.color) }}>
      {tag.name}
    </span>
  )
}

function getReadableTextColor(backgroundColor: string) {
  const hex = backgroundColor.replace('#', '')
  if (hex.length !== 6) {
    return '#ffffff'
  }

  const red = Number.parseInt(hex.slice(0, 2), 16)
  const green = Number.parseInt(hex.slice(2, 4), 16)
  const blue = Number.parseInt(hex.slice(4, 6), 16)
  const luminance = (red * 0.299 + green * 0.587 + blue * 0.114) / 255
  return luminance > 0.65 ? '#111827' : '#ffffff'
}

function uniqueTagsById(tags: TransactionTag[]) {
  return tags.filter((x, index) => tags.findIndex(y => y.id === x.id) === index)
}

function uniqueMerchantRulesById(rules: MerchantTagRule[]) {
  return rules.filter((x, index) => rules.findIndex(y => y.id === x.id) === index)
}

function getAccountHue(accountId: string) {
  const hues = [172, 205, 237, 268, 322, 24, 48, 142]
  return `${hues[getStableIndex(accountId, hues.length)]}`
}

function getStableIndex(value: string, length: number) {
  let hash = 0

  for (const character of value) {
    hash = (hash * 31 + character.charCodeAt(0)) % length
  }

  return hash
}

function getMerchantKey(merchantName: string) {
  const ignoredTokens = new Set(['au', 'aus', 'vi', 'pty', 'ltd', 'limited', 'australia', 'melbourne', 'sydney', 'brisbane', 'card', 'com'])
  return merchantName
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
    .split(' ')
    .filter(x => x && !ignoredTokens.has(x))
    .join(' ')
}

function DateRangeFilter({ value, onChange }: { value: DateFilter; onChange: (value: DateFilter) => void }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, from: x.target.value })}
        type="date"
        value={value.from ?? ''}
      />
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, to: x.target.value })}
        type="date"
        value={value.to ?? ''}
      />
    </div>
  )
}

function AmountRangeFilter({ value, onChange }: { value: AmountFilter; onChange: (value: AmountFilter) => void }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, min: x.target.value })}
        placeholder="Min"
        type="number"
        value={value.min ?? ''}
      />
      <input
        className="h-8 min-w-0 rounded-md border border-input bg-background px-2 text-sm font-normal normal-case text-foreground outline-none focus:border-ring focus:ring-2 focus:ring-ring/30"
        onChange={x => onChange({ ...value, max: x.target.value })}
        placeholder="Max"
        type="number"
        value={value.max ?? ''}
      />
    </div>
  )
}
