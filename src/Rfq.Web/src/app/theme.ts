import { theme, type ThemeConfig } from 'antd'

export type AppThemeMode = 'Light' | 'Dark'

export function applicationTheme(mode: AppThemeMode): ThemeConfig {
  return {
    algorithm: mode === 'Dark' ? theme.darkAlgorithm : theme.defaultAlgorithm,
  }
}

export function applyGlobalTheme(mode: AppThemeMode) {
  const value = mode.toLowerCase()
  document.documentElement.dataset.appTheme = value
  document.documentElement.dataset.agThemeMode = value
  document.documentElement.style.colorScheme = value
}
