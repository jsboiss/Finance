import { Link } from '@tanstack/react-router'
import type { ReactNode } from 'react'

export function NavLink({ to, icon, label }: { to: string; icon: ReactNode; label: string }) {
  return (
    <Link to={to} className="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-muted hover:text-foreground [&.active]:bg-accent [&.active]:text-accent-foreground">
      {icon}
      {label}
    </Link>
  )
}
