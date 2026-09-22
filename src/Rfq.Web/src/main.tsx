import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { ModuleRegistry, AllCommunityModule } from 'ag-grid-community'
import { AppRouter } from './app/router'
import { store } from './app/store'
import { applyGlobalTheme } from './app/theme'
import './styles.css'

ModuleRegistry.registerModules([AllCommunityModule])
applyGlobalTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <AppRouter />
    </Provider>
  </StrictMode>,
)
