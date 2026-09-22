import { theme } from 'antd'
import { describe, expect, it } from 'vitest'
import { applicationTheme, applyGlobalTheme } from '@/app/theme'

describe('application theme', () => {
  it('uses the Ant Design dark algorithm and enables AG Grid dark mode', () => {
    applyGlobalTheme('Dark')

    expect(applicationTheme('Dark').algorithm).toBe(theme.darkAlgorithm)
    expect(document.documentElement.dataset.agThemeMode).toBe('dark')
    expect(document.documentElement.dataset.appTheme).toBe('dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('uses the Ant Design default algorithm and enables AG Grid light mode', () => {
    applyGlobalTheme('Light')

    expect(applicationTheme('Light').algorithm).toBe(theme.defaultAlgorithm)
    expect(document.documentElement.dataset.agThemeMode).toBe('light')
    expect(document.documentElement.dataset.appTheme).toBe('light')
    expect(document.documentElement.style.colorScheme).toBe('light')
  })
})
