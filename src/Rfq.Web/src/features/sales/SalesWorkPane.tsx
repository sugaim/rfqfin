import {
  Button,
  Descriptions,
  Input,
  Popconfirm,
  Select,
  Space,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { SalesRfq } from '@/services/api'
import {
  displayState,
  elapsedLabel,
  type SalesCommand,
} from '@/features/sales/salesModel'

type UserOption = { userId: string; name: string }

const million = 1_000_000

function quoteValue(value: number | null | undefined, suffix = '') {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString()}${suffix}`
}

function compactText(value: string, empty = '—') {
  return value.trim() || empty
}

interface WorkPaneHeaderProps {
  mode: string
  row?: SalesRfq
}

export function WorkPaneHeader({
  mode,
  row,
}: WorkPaneHeaderProps): ReactElement {
  const title =
    mode === 'new' ? 'New RFQ' : row ? `Case ${row.caseId}` : 'Work Pane'

  return (
    <div className="work-pane-header">
      <div>
        <Typography.Text strong>{title}</Typography.Text>
        {row && (
          <div className="work-pane-security">
            {row.clientName} · {row.securityJapaneseName}
          </div>
        )}
      </div>
      <Tag>{mode.toUpperCase()}</Tag>
    </div>
  )
}

interface QuoteSummaryProps {
  row: SalesRfq
}

function QuoteSummary({ row }: QuoteSummaryProps): ReactElement | null {
  const quote = row.confirmedQuote
  if (!quote) return null

  return (
    <div className="quote-summary">
      <div>
        <span>Price</span>
        <strong>{quoteValue(quote.price)}</strong>
      </div>
      <div>
        <span>Yield</span>
        <strong>{quoteValue(quote.bbgYield, '%')}</strong>
      </div>
      <div>
        <span>Simple</span>
        <strong>{quoteValue(quote.finalSimpleYield, '%')}</strong>
      </div>
      <div>
        <span>G-Spread</span>
        <strong>{quoteValue(quote.gSpread, ' bp')}</strong>
      </div>
    </div>
  )
}

interface ContactOwnerControl {
  users: UserOption[]
  currentUserId: string
  targetContactOwnerId?: string
  onTargetContactOwnerChange: (value: string) => void
  onChangeContactOwner: () => void
}

interface MemoEditorControl {
  memoEditing: boolean
  memoDraft: string
  onMemoEdit: () => void
  onMemoChange: (value: string) => void
  onMemoCancel: () => void
  onMemoSave: () => void
}

interface LifecyclePaneProps {
  row: SalesRfq
  mode: string
  now: number
  isMutating: boolean
  contactOwner: ContactOwnerControl
  memo: MemoEditorControl
  onCorrectOutcome: () => void
  onCommand: (command: SalesCommand) => void
}

export function LifecyclePane({
  row,
  mode,
  now,
  isMutating,
  contactOwner,
  memo,
  onCorrectOutcome,
  onCommand,
}: LifecyclePaneProps): ReactElement {
  const {
    users,
    currentUserId,
    targetContactOwnerId,
    onTargetContactOwnerChange,
    onChangeContactOwner,
  } = contactOwner
  const {
    memoEditing,
    memoDraft,
    onMemoEdit,
    onMemoChange,
    onMemoCancel,
    onMemoSave,
  } = memo
  const amendment = row.draftRevisionId
    ? [
        row.draftNotional !== null &&
        row.draftNotional !== undefined &&
        row.draftNotional !== row.notional
          ? `Notl ${quoteValue(row.notional && row.notional / million, ' MM')} → ${quoteValue(row.draftNotional / million, ' MM')}`
          : null,
        row.draftSettlementDate &&
        row.draftSettlementDate !== row.settlementDate
          ? `Settle ${row.settlementDate} → ${row.draftSettlementDate}`
          : null,
        row.draftSalesAndTradingMessage !== null &&
        row.draftSalesAndTradingMessage !== undefined &&
        row.draftSalesAndTradingMessage !== row.salesAndTradingMessage
          ? 'Message changed'
          : null,
      ].filter(Boolean)
    : []

  return (
    <>
      <Descriptions
        size="small"
        column={1}
        colon={false}
        className="work-pane-facts"
        items={[
          {
            key: 'state',
            label: 'State',
            children: (
              <Space>
                <Tag>{displayState(row)}</Tag>
                {row.draftRevisionId && <Tag color="purple">AMEND</Tag>}
              </Space>
            ),
          },
          {
            key: 'notl',
            label: 'Notl',
            children:
              row.notional == null
                ? '—'
                : `${(row.notional / million).toLocaleString()} MM`,
          },
          {
            key: 'settle',
            label: 'Settle',
            children: row.settlementDate ?? '—',
          },
          { key: 'trader', label: 'Trader', children: row.assignedTraderId },
          {
            key: 'owner',
            label: 'Owner',
            children:
              users.find((user) => user.userId === row.contactOwnerId)?.name ??
              row.contactOwnerId,
          },
          {
            key: 'elapsed',
            label: 'Elapsed',
            children: `${elapsedLabel(row.stateSince, now)} · ${row.quoteRequestReason ?? 'current state'}`,
          },
          {
            key: 'message',
            label: 'Message',
            children: compactText(row.salesAndTradingMessage),
          },
        ]}
      />
      {(mode === 'quoted' ||
        mode === 'presented' ||
        mode === 'hit' ||
        mode === 'away' ||
        mode === 'cancelled') && <QuoteSummary row={row} />}
      {amendment.length > 0 && (
        <div className="amendment-diff">
          <Typography.Text strong>Pending Amendment</Typography.Text>
          {amendment.map((value) => (
            <div key={value}>{value}</div>
          ))}
          <Space>
            <Button
              size="small"
              type="primary"
              onClick={() => onCommand('confirm-amendment')}
            >
              Confirm Amendment
            </Button>
            <Button size="small" onClick={() => onCommand('discard-amendment')}>
              Discard
            </Button>
          </Space>
        </div>
      )}
      <div className="work-pane-actions">
        {mode === 'waiting' && (
          <Button size="small" danger onClick={() => onCommand('cancel')}>
            Cancel
          </Button>
        )}
        {mode === 'quoted' && (
          <>
            <Button
              size="small"
              type="primary"
              onClick={() => onCommand('present')}
            >
              Present
            </Button>
            <Button size="small" onClick={() => onCommand('hit')}>
              Hit
            </Button>
            <Button size="small" onClick={() => onCommand('away')}>
              Away
            </Button>
            <Button size="small" danger onClick={() => onCommand('cancel')}>
              Cancel
            </Button>
          </>
        )}
        {mode === 'presented' && (
          <>
            <Button
              size="small"
              type="primary"
              onClick={() => onCommand('hit')}
            >
              Hit
            </Button>
            <Button
              size="small"
              type="primary"
              onClick={() => onCommand('away')}
            >
              Away
            </Button>
            <Button size="small" onClick={() => onCommand('unpresent')}>
              Unpresent
            </Button>
            <Button size="small" danger onClick={() => onCommand('cancel')}>
              Cancel
            </Button>
          </>
        )}
        {mode === 'cancelled' && (
          <Button
            size="small"
            type="primary"
            onClick={() => onCommand('reopen')}
          >
            Reopen
          </Button>
        )}
        {(mode === 'hit' || mode === 'away') && (
          <Button size="small" type="text" onClick={onCorrectOutcome}>
            Correct outcome
          </Button>
        )}
        <Button
          size="small"
          type="text"
          onClick={() => onCommand('create-from-existing')}
        >
          Create New from Existing
        </Button>
      </div>
      {row.contactOwnerId === currentUserId && (
        <div className="owner-handoff">
          <Typography.Text type="secondary">
            Contact Owner handoff
          </Typography.Text>
          <Space.Compact block>
            <Select
              size="small"
              aria-label="Contact Owner"
              value={targetContactOwnerId}
              onChange={onTargetContactOwnerChange}
              options={users
                .filter((user) => user.userId !== row.contactOwnerId)
                .map((user) => ({ value: user.userId, label: user.name }))}
            />
            <Popconfirm
              title={
                targetContactOwnerId
                  ? `Hand off Case ${row.caseId} to ${targetContactOwnerId}?`
                  : 'Select a Contact Owner.'
              }
              disabled={!targetContactOwnerId}
              onConfirm={onChangeContactOwner}
            >
              <Button
                size="small"
                disabled={!targetContactOwnerId || isMutating}
              >
                Change
              </Button>
            </Popconfirm>
          </Space.Compact>
        </div>
      )}
      <div className="memo-line">
        <Typography.Text type="secondary">Memo</Typography.Text>
        {memoEditing ? (
          <>
            <Input.TextArea
              aria-label="Sales-only Memo"
              size="small"
              autoSize={{ minRows: 2, maxRows: 4 }}
              value={memoDraft}
              onChange={(event) => onMemoChange(event.target.value)}
            />
            <Space>
              <Button
                size="small"
                type="primary"
                loading={isMutating}
                onClick={onMemoSave}
              >
                Save
              </Button>
              <Button size="small" onClick={onMemoCancel}>
                Cancel
              </Button>
            </Space>
          </>
        ) : (
          <>
            <Tooltip title={row.salesMemo}>
              <span className="memo-preview">
                {compactText(row.salesMemo, 'No memo')}
              </span>
            </Tooltip>
            <Button size="small" type="link" onClick={onMemoEdit}>
              Edit
            </Button>
          </>
        )}
      </div>
    </>
  )
}
import type { ReactElement } from 'react'
