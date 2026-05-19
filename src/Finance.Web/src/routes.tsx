import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router'
import { Shell } from './layout/Shell'
import { Accounts } from './pages/Accounts'
import { Budgets } from './pages/Budgets'
import { Imports } from './pages/Imports'
import { Overview } from './pages/Overview'
import { PayBreakdowns } from './pages/PayBreakdowns'
import { Settings } from './pages/Settings'
import { Subscriptions } from './pages/Subscriptions'
import { Transactions } from './pages/Transactions'

const rootRoute = createRootRoute({ component: Shell })
const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Overview })
const accountsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/accounts', component: Accounts })
const transactionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/transactions', component: Transactions })
const budgetsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/budgets', component: Budgets })
const payBreakdownsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/pay-breakdowns', component: PayBreakdowns })
const subscriptionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/subscriptions', component: Subscriptions })
const importsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/imports', component: Imports })
const settingsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/settings', component: Settings })

export const router = createRouter({ routeTree: rootRoute.addChildren([indexRoute, accountsRoute, transactionsRoute, budgetsRoute, payBreakdownsRoute, subscriptionsRoute, importsRoute, settingsRoute]) })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
