import { useEffect, useState } from 'react'
import {
  Alert,
  Button,
  Divider,
  Drawer,
  Segmented,
  Select,
  Space,
  Typography,
} from 'antd'
import type { AppThemeMode } from '@/app/theme'
import type { QuoteExpiry, QuoteModeSetting } from '@/services/api'

export interface PersonalSettingsDraft {
  theme: AppThemeMode
  quoteMode: QuoteModeSetting['mode']
  quoteExpiry: QuoteExpiry
}

interface SettingsDrawerProps {
  open: boolean
  theme: AppThemeMode
  quoteMode: QuoteModeSetting['mode']
  quoteExpiry: QuoteExpiry
  isLoading?: boolean
  onClose: () => void
  onSave: (settings: PersonalSettingsDraft) => Promise<void>
}

function expiryValue(expiry: QuoteExpiry) {
  return expiry.type === 'None' ? 'none' : String(expiry.minutes)
}

function expiryFromValue(value: string): QuoteExpiry {
  return value === 'none'
    ? { type: 'None', minutes: null }
    : { type: 'After', minutes: Number(value) }
}

export function SettingsDrawer({
  open,
  theme,
  quoteMode,
  quoteExpiry,
  isLoading = false,
  onClose,
  onSave,
}: SettingsDrawerProps) {
  const [draft, setDraft] = useState<PersonalSettingsDraft>({
    theme,
    quoteMode,
    quoteExpiry,
  })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    if (!open) return
    setDraft({ theme, quoteMode, quoteExpiry })
    setError(undefined)
  }, [open, quoteExpiry, quoteMode, theme])

  const save = async () => {
    setSaving(true)
    setError(undefined)
    try {
      await onSave(draft)
      onClose()
    } catch {
      setError('Personal settings could not be saved.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Drawer
      title="Settings"
      open={open}
      onClose={onClose}
      size="default"
      extra={
        <Button
          type="primary"
          loading={saving}
          disabled={isLoading}
          onClick={() => void save()}
        >
          Save
        </Button>
      }
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {error && <Alert type="error" message={error} showIcon />}
        <Typography.Title level={5}>Appearance</Typography.Title>
        <Typography.Text>Theme</Typography.Text>
        <Segmented
          block
          aria-label="Theme"
          value={draft.theme}
          options={['Light', 'Dark']}
          onChange={(value) =>
            setDraft((current) => ({
              ...current,
              theme: value as AppThemeMode,
            }))
          }
        />
        <Divider />
        <Typography.Title level={5}>Trader Preferences</Typography.Title>
        <Typography.Text>Default Quote Mode</Typography.Text>
        <Segmented
          block
          aria-label="Default Quote Mode"
          value={draft.quoteMode}
          options={['Calculated', 'Manual']}
          onChange={(value) =>
            setDraft((current) => ({
              ...current,
              quoteMode: value as QuoteModeSetting['mode'],
            }))
          }
        />
        <Typography.Text>Default Quote Expiry</Typography.Text>
        <Select
          aria-label="Default Quote Expiry"
          value={expiryValue(draft.quoteExpiry)}
          options={[
            { value: 'none', label: 'None' },
            { value: '15', label: '15 min' },
            { value: '60', label: '1 hour' },
          ]}
          onChange={(value) =>
            setDraft((current) => ({
              ...current,
              quoteExpiry: expiryFromValue(value),
            }))
          }
        />
        <Typography.Paragraph type="secondary">
          Quote defaults apply only to future quote work and confirmations.
        </Typography.Paragraph>
      </Space>
    </Drawer>
  )
}
