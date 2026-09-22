import { useMemo, useState } from 'react'
import { Alert, Button, Card, Drawer, Input, InputNumber, Popconfirm, Select, Space, Spin, Tag, Typography, message } from 'antd'
import type { CellEditRequestEvent, ColDef, RowClickedEvent } from 'ag-grid-community'
import { AgGridReact } from 'ag-grid-react'
import type { BulkItemResult, TraderRfq } from '../../services/api'

type RfqOutcome = 'Hit' | 'Away'
type UserOption = { userId: string; name: string }
type CloseItem = { caseId: number; expectedCurrentVersion: number }

const million = 1_000_000
const formatPercent = (value: unknown) =>
  value == null ? '' : `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })}%`
const formatBasisPoints = (value: unknown) =>
  value == null ? '' : `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 })} bp`

export interface TraderScreenProps {
  rfqs: TraderRfq[]
  traders: UserOption[]
  users: UserOption[]
  currentUserId: string
  defaultExpiryMinutes: number | null
  isLoading: boolean
  isError: boolean
  isMutating: boolean
  onPickUp: (caseId: number, expectedVersion: number, confirmed: boolean) => Promise<void>
  onRelease: (caseId: number, expectedVersion: number) => Promise<void>
  onAssign: (caseId: number, targetTraderId: string, expectedVersion: number) => Promise<void>
  onTakeOver: (caseId: number, expectedVersion: number, confirmed: boolean) => Promise<void>
  onCalculate: (
    row: TraderRfq,
    driver: string,
    value: number,
    simpleYieldSlide: number,
  ) => Promise<void>
  onChangeMode: (row: TraderRfq, mode: 'Calculated' | 'Manual') => Promise<void>
  onUpdateManual: (
    row: TraderRfq,
    price: number | null,
    finalSimpleYield: number | null,
  ) => Promise<void>
  onConfirmQuote: (row: TraderRfq, expiryMinutes: number | null) => Promise<void>
  onClose: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onBulkClose: (items: CloseItem[]) => Promise<BulkItemResult[]>
  onCorrectOutcome: (caseId: number, outcome: RfqOutcome, expectedCurrentVersion: number) => Promise<void>
  onChangeContactOwner: (caseId: number, targetUserId: string, expectedCurrentVersion: number) => Promise<void>
  onUpdateMemo: (caseId: number, memo: string, expectedVersion: number) => Promise<void>
  onWithdraw?: (row: TraderRfq) => Promise<void>
  onScratchPrice?: (input: { securityId: string; settlementDate: string; driver: string; value: number; simpleYieldSlide: number }) => Promise<unknown>
  onReload: () => void | Promise<unknown>
}

export function TraderScreen({
  rfqs,
  traders,
  users,
  currentUserId,
  defaultExpiryMinutes,
  isLoading,
  isError,
  isMutating,
  onPickUp,
  onRelease,
  onAssign,
  onTakeOver,
  onCalculate,
  onChangeMode,
  onUpdateManual,
  onConfirmQuote,
  onClose,
  onBulkClose,
  onCorrectOutcome,
  onChangeContactOwner,
  onUpdateMemo,
  onWithdraw,
  onScratchPrice,
  onReload,
}: TraderScreenProps) {
  const [selected, setSelected] = useState<TraderRfq | null>(null)
  const [selectedRows, setSelectedRows] = useState<TraderRfq[]>([])
  const [targetTraderId, setTargetTraderId] = useState<string>()
  const [targetContactOwnerId, setTargetContactOwnerId] = useState<string>()
  const [memoDraft, setMemoDraft] = useState('')
  const [actionError, setActionError] = useState(false)
  const [calculationStatus, setCalculationStatus] = useState<Record<number, string>>({})
  const [expirySelections, setExpirySelections] = useState<Record<number, string>>({})
  const [pricerOpen, setPricerOpen] = useState(false)
  const [scratchSecurity, setScratchSecurity] = useState('')
  const [scratchSettlement, setScratchSettlement] = useState('')
  const [scratchValue, setScratchValue] = useState<number>(100)
  const [scratchResult, setScratchResult] = useState<unknown>()
  const canEditQuote = (row?: TraderRfq) => Boolean(
    row
    && row.owned
    && row.assignedTraderId === currentUserId
    && row.quoteStatus === 'Requested',
  )
  const columns = useMemo<ColDef<TraderRfq>[]>(() => [
    { field: 'caseId', headerName: 'Case ID', minWidth: 110 },
    { field: 'clientName', headerName: 'Client', minWidth: 180 },
    { field: 'securityJapaneseName', headerName: 'Security Name', minWidth: 210 },
    { field: 'securityBbgDisplay', headerName: 'BBG Display', minWidth: 220 },
    { field: 'categoryId', headerName: 'Category', minWidth: 110 },
    {
      field: 'notional',
      headerName: 'Notional (MM)',
      minWidth: 140,
      valueFormatter: ({ value }) => value == null ? '' : (Number(value) / million).toLocaleString(),
    },
    { field: 'assignedTraderId', headerName: 'Assigned Trader', minWidth: 150 },
    { field: 'owned', headerName: 'Owned', minWidth: 100, valueFormatter: ({ value }) => value ? 'Yes' : 'No' },
    { field: 'rfqStatus', headerName: 'RFQ Status', minWidth: 130 },
    { field: 'quoteStatus', headerName: 'Quote Status', minWidth: 130 },
    { field: 'quoteRequestReason', headerName: 'Reason', minWidth: 120 },
    { field: 'settlementDate', headerName: 'Settlement', minWidth: 130 },
    { field: 'workingQuoteMode', headerName: 'Mode', minWidth: 110 },
    {
      field: 'currentQuoteId',
      headerName: 'Confirmed Quote',
      minWidth: 210,
      valueGetter: ({ data }) => data?.currentQuoteId ?? data?.closedQuoteId,
      valueFormatter: ({ value }) => value ? String(value).slice(0, 8) : '',
    },
    { field: 'confirmedAt', headerName: 'Confirmed At', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : '' },
    { field: 'expiresAt', headerName: 'Expires At', minWidth: 190, valueFormatter: ({ value }) => value ? new Date(String(value)).toLocaleString() : 'None' },
    {
      colId: 'price',
      headerName: 'Price',
      minWidth: 110,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Manual'
        ? data.manual?.price
        : data?.calculated?.price,
      editable: ({ data }) => canEditQuote(data),
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'bbgYield',
      headerName: 'BBG Yield (%)',
      minWidth: 120,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.bbgYield
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'baseSimpleYield',
      headerName: 'Simple Yield (%)',
      minWidth: 130,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.baseSimpleYield
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'gSpread',
      headerName: 'G-Spread (bp)',
      minWidth: 120,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.gSpread
        : undefined,
      valueFormatter: ({ value }) => formatBasisPoints(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated',
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'simpleYieldSlide',
      headerName: 'SY Slide (%)',
      minWidth: 110,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Calculated'
        ? data.calculated?.simpleYieldSlide ?? 0
        : undefined,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Calculated' && data?.calculated != null,
      cellEditor: 'agNumberCellEditor',
    },
    {
      colId: 'finalSimpleYield',
      headerName: 'Final Simple Yield (%)',
      minWidth: 150,
      valueGetter: ({ data }) => data?.workingQuoteMode === 'Manual'
        ? data.manual?.finalSimpleYield
        : data?.calculated?.finalSimpleYield,
      valueFormatter: ({ value }) => formatPercent(value),
      editable: ({ data }) => canEditQuote(data) && data?.workingQuoteMode === 'Manual',
      cellEditor: 'agNumberCellEditor',
    },
    {
      headerName: 'Calc Status',
      minWidth: 120,
      valueGetter: ({ data }) => data ? calculationStatus[data.caseId] ?? 'Idle' : '',
    },
  ], [calculationStatus, currentUserId])

  const runAction = async (action: () => Promise<void>) => {
    setActionError(false)
    try {
      await action()
      setSelected(null)
      setSelectedRows([])
      setTargetTraderId(undefined)
      setTargetContactOwnerId(undefined)
      setMemoDraft('')
      await onReload()
    } catch {
      setActionError(true)
      void message.error('Conflict or validation error. The latest RFQ has been reloaded.')
      await onReload()
    }
  }

  const pickUpTargets = selectedRows.length > 1
    ? selectedRows.filter((row) => !row.owned && row.rfqStatus !== 'Hit' && row.rfqStatus !== 'Away')
    : selected && !selected.owned && selected.rfqStatus !== 'Hit' && selected.rfqStatus !== 'Away'
      ? [selected]
      : []
  const pickUp = () => runAction(async () => {
    await Promise.all(pickUpTargets.map((row) =>
      onPickUp(
        row.caseId,
        row.currentVersion,
        pickUpTargets.length > 1 || row.assignedTraderId !== currentUserId,
      )))
  })
  const release = () => selected && runAction(() =>
    onRelease(selected.caseId, selected.currentVersion))
  const assign = () => selected && targetTraderId && runAction(() =>
    onAssign(selected.caseId, targetTraderId, selected.currentVersion))
  const takeOver = () => selected && runAction(() =>
    onTakeOver(selected.caseId, selected.currentVersion, true))

  const editQuote = async (event: CellEditRequestEvent<TraderRfq>) => {
    const row = event.data
    const value = Number(event.newValue)
    if (!Number.isFinite(value)) {
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      return
    }

    setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Calculating' }))
    try {
      if (row.workingQuoteMode === 'Manual') {
        await onUpdateManual(
          row,
          event.column.getColId() === 'price' ? value : row.manual?.price ?? null,
          event.column.getColId() === 'finalSimpleYield'
            ? value
            : row.manual?.finalSimpleYield ?? null,
        )
      } else {
        const drivers: Record<string, string> = {
          price: 'Price',
          bbgYield: 'BbgYield',
          baseSimpleYield: 'SimpleYield',
          gSpread: 'GSpread',
        }
        const column = event.column.getColId()
        const driver = column === 'simpleYieldSlide'
          ? row.calculated?.driver
          : drivers[column]
        const driverValue = column === 'simpleYieldSlide'
          ? row.calculated?.driverValue
          : value
        if (!driver || driverValue === undefined) return
        await onCalculate(
          row,
          driver,
          driverValue,
          column === 'simpleYieldSlide'
            ? value
            : row.calculated?.simpleYieldSlide ?? 0,
        )
      }
      setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Success' }))
      await onReload()
    } catch {
      setCalculationStatus((current) => ({ ...current, [row.caseId]: 'Error' }))
      event.api.refreshCells({ rowNodes: [event.node], force: true })
      void message.error('Calculation failed. The edited value was reverted.')
    }
  }

  const pickUpNeedsConfirmation = pickUpTargets.length > 1
    || pickUpTargets.some((row) => row.assignedTraderId !== currentUserId)
  const selectedExpiry = selected
    ? expirySelections[selected.caseId] ?? (defaultExpiryMinutes?.toString() ?? 'none')
    : undefined
  const canConfirmQuote = Boolean(
    selected
    && canEditQuote(selected)
    && (selected.workingQuoteMode === 'Calculated'
      ? selected.calculated != null
      : selected.manual?.price != null && selected.manual.finalSimpleYield != null),
  )
  const isContactOwner = selected?.contactOwnerId === currentUserId
  const isSelectedOpen = Boolean(
    selected && (selected.rfqStatus === 'Active' || selected.rfqStatus === 'Presented'),
  )
  const canClose = Boolean(isContactOwner && isSelectedOpen && selected?.quoteStatus === 'Quoted')
  const canCorrectOutcome = Boolean(
    isContactOwner && selected && (selected.rfqStatus === 'Hit' || selected.rfqStatus === 'Away'),
  )
  const bulkCloseAway = async () => {
    setActionError(false)
    try {
      const results = await onBulkClose(
        selectedRows.map((row) => ({
          caseId: row.caseId,
          expectedCurrentVersion: row.currentVersion,
        })),
      )
      const details = results.map((result) =>
        `Case ${result.caseId}: ${result.status}${result.message ? ` (${result.message})` : ''}`)
      void message.info(`Bulk Away — ${details.join('; ')}`)
      setSelected(null)
      setSelectedRows([])
      await onReload()
    } catch {
      setActionError(true)
    }
  }

  return (
    <Card
      className="trader-grid-card"
      title="RFQs"
      extra={<Button onClick={() => void onReload()}>Reload</Button>}
    >
      {isError && <Alert type="error" showIcon message="Trader RFQs could not be loaded." className="grid-alert" />}
      {actionError && <Alert type="error" showIcon message="Ownership changed or the action is not permitted. Reload and try again." className="grid-alert" />}
      <Space wrap className="ownership-actions">
        <Button onClick={() => {
          setScratchSecurity(selected?.securityId ?? '')
          setScratchSettlement(selected?.settlementDate ?? '')
          setScratchValue(selected?.calculated?.price ?? 100)
          setPricerOpen(true)
        }}>Pricer</Button>
        {pickUpNeedsConfirmation ? (
          <Popconfirm
            title={pickUpTargets.length > 1
              ? `Pick up ${pickUpTargets.length} selected RFQs?`
              : `Pick up Case ${pickUpTargets[0]?.caseId} from ${pickUpTargets[0]?.assignedTraderId}?`}
            onConfirm={() => void pickUp()}
          >
            <Button disabled={isMutating}>Pick Up</Button>
          </Popconfirm>
        ) : (
          <Button
            disabled={pickUpTargets.length === 0 || isMutating}
            onClick={() => void pickUp()}
          >
            Pick Up
          </Button>
        )}
        <Button
          disabled={!selected || !isSelectedOpen || !selected.owned || selected.assignedTraderId !== currentUserId || isMutating}
          onClick={() => void release()}
        >
          Release
        </Button>
        <Select
          aria-label="Assign to trader"
          placeholder="Assign to..."
          value={targetTraderId}
          onChange={setTargetTraderId}
          disabled={!selected || !isSelectedOpen || selected.owned || isMutating}
          options={traders.map((trader) => ({ value: trader.userId, label: trader.name }))}
          style={{ width: 180 }}
        />
        <Button
          disabled={!selected || !isSelectedOpen || selected.owned || !targetTraderId || isMutating}
          onClick={() => void assign()}
        >
          Assign
        </Button>
        <Popconfirm
          title={selected ? `Take over Case ${selected.caseId} from ${selected.assignedTraderId} as ${currentUserId}?` : 'Take over this RFQ?'}
          description="This changes the owner immediately."
          onConfirm={() => void takeOver()}
        >
          <Button
            danger
            disabled={!selected || !isSelectedOpen || !selected.owned || selected.assignedTraderId === currentUserId || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
        <Select
          aria-label="Quote mode"
          value={selected?.workingQuoteMode}
          placeholder="Quote mode"
          disabled={!selected || !canEditQuote(selected) || isMutating}
          options={[
            { value: 'Calculated', label: 'Calculated' },
            { value: 'Manual', label: 'Manual' },
          ]}
          onChange={(mode: 'Calculated' | 'Manual') => {
            if (selected) void runAction(() => onChangeMode(selected, mode))
          }}
          style={{ width: 140 }}
        />
        <Select
          aria-label="Quote expiry"
          value={selectedExpiry}
          disabled={!selected || !canEditQuote(selected) || isMutating}
          options={[
            { value: 'none', label: 'Expiry: None' },
            { value: '5', label: 'Expiry: 5 min' },
            { value: '15', label: 'Expiry: 15 min' },
            { value: '30', label: 'Expiry: 30 min' },
          ]}
          onChange={(value) => selected && setExpirySelections((current) => ({
            ...current,
            [selected.caseId]: value,
          }))}
          style={{ width: 150 }}
        />
        <Popconfirm
          title={selected ? `Confirm quote for Case ${selected.caseId}?` : 'Confirm quote?'}
          onConfirm={() => selected && void runAction(() => onConfirmQuote(
            selected,
            selectedExpiry === 'none' ? null : Number(selectedExpiry),
          ))}
        >
          <Button type="primary" disabled={!canConfirmQuote || isMutating}>
            Confirm Quote
          </Button>
        </Popconfirm>
        <Button
          disabled={!selected || !onWithdraw || selected.rfqStatus === 'Presented' || selected.quoteStatus !== 'Quoted' || !selected.owned || selected.assignedTraderId !== currentUserId || isMutating}
          onClick={() => selected && void runAction(() => onWithdraw!(selected))}
        >Withdraw</Button>
        <Popconfirm
          title={selected ? `Close Case ${selected.caseId} as Hit?` : 'Close as Hit?'}
          onConfirm={() => selected
            && void runAction(() => onClose(selected.caseId, 'Hit', selected.currentVersion))}
        >
          <Button disabled={!canClose || isMutating}>Hit</Button>
        </Popconfirm>
        <Popconfirm
          title={selected ? `Close Case ${selected.caseId} as Away?` : 'Close as Away?'}
          onConfirm={() => selected
            && void runAction(() => onClose(selected.caseId, 'Away', selected.currentVersion))}
        >
          <Button disabled={!canClose || isMutating}>Away</Button>
        </Popconfirm>
        <Popconfirm
          title={selected
            ? `Correct Case ${selected.caseId} to ${selected.rfqStatus === 'Hit' ? 'Away' : 'Hit'}?`
            : 'Correct outcome?'}
          onConfirm={() => selected
            && void runAction(() => onCorrectOutcome(
              selected.caseId,
              selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
              selected.currentVersion,
            ))}
        >
          <Button disabled={!canCorrectOutcome || isMutating}>Correct Outcome</Button>
        </Popconfirm>
        <Popconfirm
          title={`Bulk close ${selectedRows.length} selected RFQs as Away?`}
          onConfirm={() => void bulkCloseAway()}
        >
          <Button disabled={selectedRows.length === 0 || isMutating}>Bulk Away</Button>
        </Popconfirm>
      </Space>
      <Spin spinning={isLoading}>
        <div className="rfq-grid" data-testid="trader-rfq-grid">
          <AgGridReact<TraderRfq>
            rowData={rfqs}
            columnDefs={columns}
            getRowId={({ data }) => String(data.caseId)}
            onRowClicked={({ data }) => {
              setSelected(data ?? null)
              setTargetTraderId(undefined)
              setTargetContactOwnerId(undefined)
              setMemoDraft(data?.traderMemo ?? '')
              setActionError(false)
            }}
            onSelectionChanged={({ api: gridApi }) =>
              setSelectedRows(gridApi.getSelectedRows())}
            readOnlyEdit
            onCellEditRequest={(event) => void editQuote(event)}
            rowSelection={{ mode: 'multiRow' }}
            defaultColDef={{ sortable: true, filter: true, resizable: true }}
          />
        </div>
      </Spin>
      {selected && (
        <Card size="small" title={`Case ${selected.caseId} details`} className="case-details">
          <Space wrap align="end">
            <div>
              <Typography.Text type="secondary">Contact Owner</Typography.Text>
              <br />
              <Select
                aria-label="Contact Owner"
                value={targetContactOwnerId}
                placeholder={selected.contactOwnerId}
                onChange={setTargetContactOwnerId}
                disabled={!isContactOwner || isMutating}
                options={users.map((user) => ({ value: user.userId, label: user.name }))}
                style={{ width: 190 }}
              />
            </div>
            <Popconfirm
              title={targetContactOwnerId
                ? `Hand off Case ${selected.caseId} to ${targetContactOwnerId}?`
                : 'Select a Contact Owner.'}
              onConfirm={() => targetContactOwnerId
                && void runAction(() => onChangeContactOwner(
                  selected.caseId,
                  targetContactOwnerId,
                  selected.currentVersion,
                ))}
            >
              <Button disabled={!isContactOwner || !targetContactOwnerId || isMutating}>
                Change Contact Owner
              </Button>
            </Popconfirm>
          </Space>
          <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
            Trader-only Memo
          </Typography.Paragraph>
          <Input.TextArea
            aria-label="Trader-only Memo"
            rows={3}
            value={memoDraft}
            onChange={(event) => setMemoDraft(event.target.value)}
          />
          <Button
            style={{ marginTop: 8 }}
            loading={isMutating}
            onClick={() => void runAction(() => onUpdateMemo(
              selected.caseId,
              memoDraft,
              selected.traderMemoVersion,
            ))}
          >
            Save Trader Memo
          </Button>
        </Card>
      )}
      <Drawer title="Independent Pricer" open={pricerOpen} onClose={() => setPricerOpen(false)}>
        <Typography.Paragraph type="secondary">Scratch values are independent and are never applied back to the RFQ.</Typography.Paragraph>
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input aria-label="Pricer Security" placeholder="Security ID" value={scratchSecurity} onChange={(event) => setScratchSecurity(event.target.value)} />
          <Input aria-label="Pricer Settlement" type="date" value={scratchSettlement} onChange={(event) => setScratchSettlement(event.target.value)} />
          <InputNumber aria-label="Pricer Value" value={scratchValue} onChange={(value) => setScratchValue(Number(value ?? 0))} style={{ width: '100%' }} />
          <Button type="primary" disabled={!onScratchPrice || !scratchSecurity || !scratchSettlement} onClick={() => onScratchPrice && void onScratchPrice({ securityId: scratchSecurity, settlementDate: scratchSettlement, driver: 'Price', value: scratchValue, simpleYieldSlide: 0 }).then(setScratchResult)}>Calculate</Button>
          {scratchResult != null && <pre>{JSON.stringify(scratchResult, null, 2)}</pre>}
        </Space>
      </Drawer>
    </Card>
  )
}
