export const currency = (amount: number | null | undefined, code: string) =>
  amount == null
    ? 'Unavailable'
    : new Intl.NumberFormat('en-AU', { style: 'currency', currency: code }).format(amount / 100)
