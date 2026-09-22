import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { DailyReviewWorkspace } from '../features/daily-review/DailyReviewWorkspace'
import { SalesWorkspace } from '../features/sales/SalesWorkspace'
import { TraderWorkspace } from '../features/trader/TraderWorkspace'
import { App } from './App'

export function defaultWorkspacePath() {
  const identity = window.localStorage.getItem('rfq-development-user') ?? 'sales-dev'
  return identity.startsWith('trader-') ? '/trader' : '/sales'
}

function DefaultWorkspaceRoute() {
  return <Navigate to={defaultWorkspacePath()} replace />
}

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<App />}>
        <Route index element={<DefaultWorkspaceRoute />} />
        <Route path="sales" element={<SalesWorkspace />} />
        <Route path="trader" element={<TraderWorkspace />} />
        <Route path="daily-review" element={<DailyReviewWorkspace />} />
        <Route path="*" element={<DefaultWorkspaceRoute />} />
      </Route>
    </Routes>
  )
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  )
}
