import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router'
import { Shell } from './layout/Shell'
import { Accounts } from './pages/Accounts'
import { Imports } from './pages/Imports'
import { Overview } from './pages/Overview'
import { Transactions } from './pages/Transactions'

const rootRoute = createRootRoute({ component: Shell })
const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Overview })
const accountsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/accounts', component: Accounts })
const transactionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/transactions', component: Transactions })
const importsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/imports', component: Imports })

export const router = createRouter({ routeTree: rootRoute.addChildren([indexRoute, accountsRoute, transactionsRoute, importsRoute]) })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
