import type { ReactElement } from 'react'
import { Button, Space, Tag, Typography } from 'antd'
import type { ColDef, ICellRendererParams } from 'ag-grid-community'
import type {
  PostProcessItem,
  PostProcessLifecycleChangeType,
} from '@/services/api'
import {
  isCorrection,
  stagedState,
  type PendingPostProcessChange,
} from '@/features/post-process/postProcessModel'

const million = 1_000_000

export interface BuildPostProcessColumnsOptions {
  currentUserId: string
  pending: Record<number, PendingPostProcessChange>
  onStageLifecycle: (
    row: PostProcessItem,
    type: PostProcessLifecycleChangeType,
  ) => void
  onClear: (caseId: number) => void
}

export function buildPostProcessColumns({
  currentUserId,
  pending,
  onStageLifecycle,
  onClear,
}: BuildPostProcessColumnsOptions): ColDef<PostProcessItem>[] {
  const actionRenderer = ({
    data,
  }: ICellRendererParams<PostProcessItem>): ReactElement | null => {
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
            onClick={() => onStageLifecycle(data, button.type)}
          >
            {button.label}
          </Button>
        ))}
        {pending[data.caseId] && (
          <Button size="small" type="text" onClick={() => onClear(data.caseId)}>
            Clear
          </Button>
        )}
      </Space>
    )
  }

  return [
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
    { field: 'lastChangedAt', headerName: 'Last Changed At', width: 180 },
    {
      colId: 'action',
      headerName: 'Action',
      minWidth: 260,
      pinned: 'right',
      cellRenderer: actionRenderer,
    },
  ]
}
