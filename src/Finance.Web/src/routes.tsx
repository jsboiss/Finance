import { createRootRoute, createRoute, createRouter } from '@tanstack/react-router'
import { lazy } from 'react'
import { Shell } from './layout/Shell'

const Overview = lazy(() => import('./pages/Overview').then(x => ({ default: x.Overview })))
const Accounts = lazy(() => import('./pages/Accounts').then(x => ({ default: x.Accounts })))
const Transactions = lazy(() => import('./pages/Transactions').then(x => ({ default: x.Transactions })))
const Budgets = lazy(() => import('./pages/Budgets').then(x => ({ default: x.Budgets })))
const SpendingPlanner = lazy(() => import('./pages/SpendingPlanner').then(x => ({ default: x.SpendingPlanner })))
const PayBreakdowns = lazy(() => import('./pages/PayBreakdowns').then(x => ({ default: x.PayBreakdowns })))
const HomeLoans = lazy(() => import('./pages/HomeLoans').then(x => ({ default: x.HomeLoans })))
const Subscriptions = lazy(() => import('./pages/Subscriptions').then(x => ({ default: x.Subscriptions })))
const Imports = lazy(() => import('./pages/Imports').then(x => ({ default: x.Imports })))
const Settings = lazy(() => import('./pages/Settings').then(x => ({ default: x.Settings })))

const rootRoute = createRootRoute({ component: Shell })
const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Overview })
const accountsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/accounts', component: Accounts })
const transactionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/transactions', component: Transactions })
const budgetsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/budgets', component: Budgets })
const spendingPlannerRoute = createRoute({ getParentRoute: () => rootRoute, path: '/spending-planner', component: SpendingPlanner })
const payBreakdownsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/pay-breakdowns', component: PayBreakdowns })
const homeLoansRoute = createRoute({ getParentRoute: () => rootRoute, path: '/home-loans', component: HomeLoans })
const subscriptionsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/subscriptions', component: Subscriptions })
const importsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/imports', component: Imports })
const settingsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/settings', component: Settings })

export const router = createRouter({ routeTree: rootRoute.addChildren([indexRoute, accountsRoute, transactionsRoute, budgetsRoute, spendingPlannerRoute, payBreakdownsRoute, homeLoansRoute, subscriptionsRoute, importsRoute, settingsRoute]) })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
