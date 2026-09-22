import { theme, type ThemeConfig } from 'antd'

export const applicationTheme: ThemeConfig = {
  algorithm: theme.darkAlgorithm,
}

export function applyGlobalTheme() {
  document.documentElement.dataset.agThemeMode = 'dark'
}
