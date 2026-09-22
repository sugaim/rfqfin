import { useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Button,
  Modal,
  Popconfirm,
  Segmented,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd'
import type {
  CellEditRequestEvent,
  ColDef,
  ICellRendererParams,
  RowClassParams,
} from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type {
  BulkItemResult,
  PostProcessCommitItem,
  PostProcessItem,
  PostProcessLifecycleChangeType,
  PostProcessPreset,
  PostProcessScope,
} from '@/services/api'
import {
  isCorrection,
  rowClass,
  stagedState,
  toCommitItem,
  type PendingPostProcessChange,
} from '@/features/post-process/postProcessModel'

const million = 1_000_000

export interface PostProcessScreenProps {
  items: PostProcessItem[]
  currentUserId: string
  preset: PostProcessPreset
  scope: PostProcessScope
  isLoading: boolean
  isError: boolean
  isCommitting: boolean
  onPresetChange: (value: PostProcessPreset) => void
  onScopeChange: (value: PostProcessScope) => void
  onRefresh: () => Promise<void>
  onCommit: (items: PostProcessCommitItem[]) => Promise<BulkItemResult[]>
  onPendingChange?: (hasPending: boolean) => void
}

export function PostProcessScreen({
  items,
  currentUserId,
  preset,
  scope,
  isLoading,
  isError,
  isCommitting,
  onPresetChange,
  onScopeChange,
  onRefresh,
  onCommit,
  onPendingChange,
}: PostProcessScreenProps) {
  const [pending, setPending] = useState<
    Record<number, PendingPostProcessChange>
  >({})
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [result, setResult] = useState<BulkItemResult[] | null>(null)
  const [resultExpanded, setResultExpanded] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [knownRows, setKnownRows] = useState<Record<number, PostProcessItem>>(
    {},
  )
  const pendingChanges = Object.values(pending)
  const pendingRows = pendingChanges
    .map((change) => ({
      change,
      row: knownRows[change.caseId],
    }))
    .filter(
      (
        value,
      ): value is {
        change: PendingPostProcessChange
        row: PostProcessItem
      } => Boolean(value.row),
    )
  const invalidCorrection = pendingChanges.some(
    (change) =>
      change.lifecycle &&
      isCorrection(change.lifecycle.type) &&
      !change.lifecycle.correctionReason?.trim(),
  )

  useEffect(() => {
    setKnownRows((current) => ({
      ...current,
      ...Object.fromEntries(items.map((item) => [item.caseId, item])),
    }))
  }, [items])

  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (!pendingChanges.length) return
      event.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [pendingChanges.length])

  useEffect(() => {
    onPendingChange?.(pendingChanges.length > 0)
  }, [onPendingChange, pendingChanges.length])

  const updatePending = (
    row: PostProcessItem,
    update: (current: PendingPostProcessChange) => PendingPostProcessChange,
  ) =>
    setPending((current) => {
      const next = update(
        current[row.caseId] ?? {
          caseId: row.caseId,
          baseCurrentVersion: row.currentVersion,
        },
      )
      if (!next.lifecycle && !next.memo) {
        const rest = { ...current }
        delete rest[row.caseId]
        return rest
      }
      return { ...current, [row.caseId]: next }
    })

  const stageLifecycle = (
    row: PostProcessItem,
    type: PostProcessLifecycleChangeType,
  ) =>
    updatePending(row, (current) => ({
      ...current,
      lifecycle: {
        type,
        correctionReason: isCorrection(type)
          ? current.lifecycle?.correctionReason
          : undefined,
      },
    }))

  const edit = (event: CellEditRequestEvent<PostProcessItem>) => {
    const row = event.data
    const column = event.column.getColId()
    if (column === 'myMemo') {
      const value = String(event.newValue ?? '')
      updatePending(row, (current) => ({
        ...current,
        memo:
          value === row.myMemo
            ? undefined
            : { baseVersion: row.myMemoVersion, value },
      }))
    } else if (
      column === 'correctionReason' &&
      pending[row.caseId]?.lifecycle &&
      isCorrection(pending[row.caseId].lifecycle!.type)
    ) {
      const value = String(event.newValue ?? '')
      updatePending(row, (current) => ({
        ...current,
        lifecycle: { ...current.lifecycle!, correctionReason: value },
      }))
    }
  }

  const actionRenderer = ({ data }: ICellRendererParams<PostProcessItem>) => {
    if (!data) return null
    const canChangeLifecycle = data.contactOwnerId === currentUserId
    const buttons: { label: string; type: PostProcessLifecycleChangeType }[] =
      data.rfqStatus === 'Active' || data.rfqStatus === 'Presented'
        ? [
            { label: 'Hit', type: 'Hit' },
            { label: 'Away', type: 'Away' },
            { label: 'Cancel', type: 'Cancel' },
          ]
        : data.rfqStatus === 'Hit'
          ? [{ label: 'Correct to Away', type: 'CorrectToAway' }]
          : data.rfqStatus === 'Away'
            ? [{ label: 'Correct to Hit', type: 'CorrectToHit' }]
            : []
    return (
      <Space size={4}>
        {buttons.map((button) => (
          <Button
            key={button.type}
            size="small"
            disabled={!canChangeLifecycle}
            onClick={() => stageLifecycle(data, button.type)}
          >
            {button.label}
          </Button>
        ))}
        {pending[data.caseId] && (
          <Button
            size="small"
            type="text"
            onClick={() =>
              setPending((current) => {
                const rest = { ...current }
                delete rest[data.caseId]
                return rest
              })
            }
          >
            Clear
          </Button>
        )}
      </Space>
    )
  }

  const columns = useMemo<ColDef<PostProcessItem>[]>(
    () => [
      { field: 'caseId', headerName: 'Case', width: 80, pinned: 'left' },
      {
        field: 'createdAt',
        headerName: 'Time',
        width: 105,
        valueFormatter: ({ value }) =>
          value ? new Date(String(value)).toLocaleTimeString() : '',
      },
      { field: 'clientName', headerName: 'Client', minWidth: 140 },
      {
        field: 'securityName',
        headerName: 'Security',
        minWidth: 180,
        tooltipField: 'securityBbgDisplay',
      },
      {
        field: 'notional',
        headerName: 'Notl (MM)',
        width: 105,
        valueFormatter: ({ value }) =>
          value == null ? '' : String(Number(value) / million),
      },
      { field: 'contactOwnerId', headerName: 'Contact Owner', width: 130 },
      { field: 'salesId', headerName: 'Sales', width: 110 },
      { field: 'assignedTraderId', headerName: 'Trader', width: 110 },
      {
        colId: 'state',
        headerName: 'State',
        width: 145,
        valueGetter: ({ data }) => {
          if (!data) return ''
          const next = stagedState(pending[data.caseId])
          return next ? `${data.rfqStatus} -> ${next}` : data.rfqStatus
        },
        cellRenderer: ({ data }: ICellRendererParams<PostProcessItem>) => {
          if (!data) return null
          const next = stagedState(pending[data.caseId])
          const color =
            data.rfqStatus === 'Hit'
              ? 'success'
              : data.rfqStatus === 'Away'
                ? 'error'
                : data.rfqStatus === 'Cancelled'
                  ? 'default'
                  : 'warning'
          return (
            <Space size={3}>
              <Tag color={color}>{data.rfqStatus.toUpperCase()}</Tag>
              {next && <Typography.Text>-&gt; {next}</Typography.Text>}
            </Space>
          )
        },
      },
      { field: 'price', headerName: 'Px', width: 85 },
      { field: 'finalSimpleYield', headerName: 'Final SY', width: 95 },
      { field: 'yield', headerName: 'Yld', width: 85, hide: true },
      { field: 'ysc', headerName: 'YSC', width: 85, hide: true },
      { field: 'gSpread', headerName: 'GSpd', width: 85, hide: true },
      {
        field: 'salesAndTradingMessage',
        headerName: 'Message',
        minWidth: 160,
      },
      {
        colId: 'myMemo',
        headerName: 'My Memo',
        minWidth: 170,
        editable: true,
        cellEditor: 'agTextCellEditor',
        valueGetter: ({ data }) =>
          data ? (pending[data.caseId]?.memo?.value ?? data.myMemo) : '',
      },
      {
        colId: 'correctionReason',
        headerName: 'Correction Reason',
        minWidth: 180,
        editable: ({ data }) =>
          Boolean(
            data &&
            pending[data.caseId]?.lifecycle &&
            isCorrection(pending[data.caseId].lifecycle!.type),
          ),
        cellEditor: 'agTextCellEditor',
        valueGetter: ({ data }) =>
          data
            ? pending[data.caseId]?.lifecycle &&
              isCorrection(pending[data.caseId].lifecycle!.type)
              ? (pending[data.caseId].lifecycle!.correctionReason ?? '')
              : (data.lastCorrectionReason ?? '')
            : '',
      },
      { field: 'lastChangedBy', headerName: 'Last Changed By', width: 140 },
      {
        field: 'lastChangedAt',
        headerName: 'Last Changed At',
        width: 180,
      },
      {
        colId: 'action',
        headerName: 'Action',
        minWidth: 260,
        pinned: 'right',
        cellRenderer: actionRenderer,
      },
    ],
    [currentUserId, pending],
  )

  const commit = async () => {
    setError(null)
    try {
      const results = await onCommit(pendingChanges.map(toCommitItem))
      setResult(results)
      setResultExpanded(false)
      const succeeded = new Set(
        results
          .filter((item) => item.status === 'Succeeded')
          .map((item) => item.caseId),
      )
      setPending((current) =>
        Object.fromEntries(
          Object.entries(current).filter(
            ([caseId]) => !succeeded.has(Number(caseId)),
          ),
        ),
      )
      setConfirmOpen(false)
    } catch {
      setError('Post Process changes could not be committed.')
    }
  }

  const refresh = async () => {
    setPending({})
    await onRefresh()
  }

  return (
    <Space orientation="vertical" size="small" style={{ width: '100%' }}>
      <div className="post-process-toolbar">
        <Space size={8}>
          <Segmented
            aria-label="Post Process preset"
            value={preset}
            options={['Today', 'Unclosed']}
            onChange={(value) => onPresetChange(value as PostProcessPreset)}
          />
          <Segmented
            aria-label="Post Process scope"
            value={scope === 'AllPermitted' ? 'All permitted' : 'Mine'}
            options={['Mine', 'All permitted']}
            onChange={(value) =>
              onScopeChange(value === 'Mine' ? 'Mine' : 'AllPermitted')
            }
          />
        </Space>
        <Space size={8}>
          <Button
            type="primary"
            disabled={!pendingChanges.length || invalidCorrection}
            onClick={() => setConfirmOpen(true)}
          >
            Confirm Changes ({pendingChanges.length})
          </Button>
          <Popconfirm
            title="Discard all uncommitted Post Process changes?"
            disabled={!pendingChanges.length}
            onConfirm={() => void refresh()}
          >
            <Button
              onClick={() => {
                if (!pendingChanges.length) void refresh()
              }}
            >
              Refresh
            </Button>
          </Popconfirm>
        </Space>
      </div>
      {invalidCorrection && (
        <Alert
          type="warning"
          title="Correction Reason is required for every staged outcome correction."
          showIcon
        />
      )}
      {error && <Alert type="error" title={error} showIcon />}
      {isError && (
        <Alert type="error" title="Post Process worklist is unavailable." />
      )}
      {result && (
        <div className="result-bar">
          <Button
            type="text"
            onClick={() => setResultExpanded(!resultExpanded)}
          >
            {result.filter((item) => item.status === 'Succeeded').length}{' '}
            succeeded /{' '}
            {result.filter((item) => item.status === 'Skipped').length} skipped
            / {result.filter((item) => item.status === 'Failed').length} failed
          </Button>
          {resultExpanded &&
            result.map((item) => (
              <div key={item.caseId}>
                Case {item.caseId}: {item.status}
                {item.code ? ` / ${item.code}` : ''}
                {item.message ? ` / ${item.message}` : ''}
              </div>
            ))}
        </div>
      )}
      <Spin spinning={isLoading}>
        <div className="rfq-grid post-process-grid">
          <AgGridReact
            rowData={items}
            columnDefs={columns}
            defaultColDef={{ sortable: true, filter: true, resizable: true }}
            readOnlyEdit
            onCellEditRequest={edit}
            rowSelection={{
              mode: 'multiRow',
              checkboxes: false,
              headerCheckbox: false,
              enableClickSelection: true,
              enableSelectionWithoutKeys: true,
            }}
            rowClassRules={{
              'post-process-row-unclosed': ({
                data,
              }: RowClassParams<PostProcessItem>) =>
                Boolean(
                  data &&
                  rowClass(data, false).includes('post-process-row-unclosed'),
                ),
              'post-process-row-cancelled': ({
                data,
              }: RowClassParams<PostProcessItem>) =>
                Boolean(
                  data &&
                  rowClass(data, false).includes('post-process-row-cancelled'),
                ),
              'post-process-row-pending': ({
                data,
              }: RowClassParams<PostProcessItem>) =>
                Boolean(data && pending[data.caseId]),
            }}
          />
        </div>
      </Spin>
      <Modal
        title={`Confirm Changes (${pendingChanges.length})`}
        open={confirmOpen}
        okText="Commit"
        confirmLoading={isCommitting}
        onOk={() => void commit()}
        onCancel={() => setConfirmOpen(false)}
      >
        <div className="post-process-confirm-table">
          <div className="post-process-confirm-row post-process-confirm-header">
            <span>Case</span>
            <span>Client / Security</span>
            <span>State</span>
            <span>Memo</span>
            <span>Correction Reason</span>
          </div>
          {pendingRows.map(({ row, change }) => (
            <div className="post-process-confirm-row" key={row.caseId}>
              <span>#{row.caseId}</span>
              <span>
                {row.clientName} / {row.securityName}
              </span>
              <span>
                {row.rfqStatus}
                {stagedState(change) ? ` -> ${stagedState(change)}` : ''}
              </span>
              <span>{change.memo ? 'Changed' : '-'}</span>
              <span>{change.lifecycle?.correctionReason?.trim() || '-'}</span>
            </div>
          ))}
        </div>
      </Modal>
    </Space>
  )
}
