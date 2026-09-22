import {
  Navigate,
  RouterProvider,
  createBrowserRouter,
  type RouteObject,
  useRoutes,
} from 'react-router'
import { PostProcessWorkspace } from '@/features/post-process/PostProcessWorkspace'
import { SalesWorkspace } from '@/features/sales/SalesWorkspace'
import { TraderWorkspace } from '@/features/trader/TraderWorkspace'
import { App } from '@/app/App'

export function defaultWorkspacePath() {
  const identity =
    window.localStorage.getItem('rfq-development-user') ?? 'sales-dev'

  return identity.startsWith('trader-') ? '/trader' : '/sales'
}

function DefaultWorkspaceRoute() {
  return <Navigate to={defaultWorkspacePath()} replace />
}

export const appRoutes: RouteObject[] = [
  {
    element: <App />,
    children: [
      { index: true, element: <DefaultWorkspaceRoute /> },
      { path: 'sales', element: <SalesWorkspace /> },
      { path: 'trader', element: <TraderWorkspace /> },
      { path: 'post-process', element: <PostProcessWorkspace /> },
      { path: '*', element: <DefaultWorkspaceRoute /> },
    ],
  },
]

export function AppRoutes() {
  return useRoutes(appRoutes)
}

const browserRouter = createBrowserRouter(appRoutes)

export function AppRouter() {
  return <RouterProvider router={browserRouter} />
}
