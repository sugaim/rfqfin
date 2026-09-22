import { theme } from 'antd'
import { describe, expect, it } from 'vitest'
import { applicationTheme, applyGlobalTheme } from '@/app/theme'

describe('application theme', () => {
  it('uses the Ant Design dark algorithm and enables AG Grid dark mode', () => {
    applyGlobalTheme()

    expect(applicationTheme.algorithm).toBe(theme.darkAlgorithm)
    expect(document.documentElement.dataset.agThemeMode).toBe('dark')
  })
})
