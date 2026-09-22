import {
  Button,
  Descriptions,
  Empty,
  Popconfirm,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd'
import type { TraderRfq } from '@/services/api'
import {
  canEditQuote,
  isPickUpEligible,
  patchContactOwner,
  patchLifecycle,
  patchOwnership,
  patchPresentation,
  patchWorkingQuote,
  requiresPickUpConfirmation,
  traderRouting,
  traderState,
  type TraderBulkCommand,
} from '@/features/trader/traderModel'
import type { TraderScreenProps } from '@/features/trader/TraderScreen'

type UserOption = { userId: string; name: string }

export function TraderOperationsPane({
  selected,
  selectedRows,
  currentUserId,
  traders,
  users,
  isMutating,
  targetTraderId,
  setTargetTraderId,
  targetContactOwnerId,
  setTargetContactOwnerId,
  onRun,
  onBulk,
  onPickUp,
  onRelease,
  onAssign,
  onTakeOver,
  onChangeMode,
  onPresent,
  onUnpresent,
  onWithdraw,
  onClose,
  onCancel,
  onReopen,
  onCorrectOutcome,
  onChangeContactOwner,
}: {
  selected?: TraderRfq
  selectedRows: TraderRfq[]
  currentUserId: string
  traders: UserOption[]
  users: UserOption[]
  isMutating: boolean
  targetTraderId?: string
  setTargetTraderId: (value?: string) => void
  targetContactOwnerId?: string
  setTargetContactOwnerId: (value?: string) => void
  onRun: <T>(
    action: () => Promise<T | void>,
    patch?: (row: TraderRfq, value: T) => TraderRfq,
    row?: TraderRfq,
  ) => Promise<T | void>
  onBulk: (
    label: string,
    command: TraderBulkCommand,
    rows: TraderRfq[],
    trader?: string,
  ) => Promise<void>
  onPickUp: TraderScreenProps['onPickUp']
  onRelease: TraderScreenProps['onRelease']
  onAssign: TraderScreenProps['onAssign']
  onTakeOver: TraderScreenProps['onTakeOver']
  onChangeMode: TraderScreenProps['onChangeMode']
  onPresent?: TraderScreenProps['onPresent']
  onUnpresent?: TraderScreenProps['onUnpresent']
  onWithdraw?: TraderScreenProps['onWithdraw']
  onClose: TraderScreenProps['onClose']
  onCancel?: TraderScreenProps['onCancel']
  onReopen?: TraderScreenProps['onReopen']
  onCorrectOutcome: TraderScreenProps['onCorrectOutcome']
  onChangeContactOwner: TraderScreenProps['onChangeContactOwner']
}) {
  if (!selected)
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Select an Active RFQ"
      />
    )
  const open = ['Active', 'Presented'].includes(selected.rfqStatus)
  const owner = selected.contactOwnerId === currentUserId
  const mine = selected.assignedTraderId === currentUserId
  const quoted = selected.quoteStatus === 'Quoted'
  const multi = selectedRows.length > 1
  const pickUpRequiresConfirmation = requiresPickUpConfirmation(
    selected,
    currentUserId,
  )
  const bulkPickRows = selectedRows.filter(isPickUpEligible)
  const otherAssignedBulkPickCount = bulkPickRows.filter((row) =>
    requiresPickUpConfirmation(row, currentUserId),
  ).length
  const runPickUp = () =>
    void onRun(
      () => onPickUp(selected, pickUpRequiresConfirmation),
      patchOwnership,
      selected,
    )
  const runBulkPick = () => void onBulk('Bulk Pick', 'pick', bulkPickRows)
  return (
    <div className="trader-operations">
      <Descriptions
        size="small"
        column={1}
        colon={false}
        items={[
          { key: 'case', label: 'Case', children: `#${selected.caseId}` },
          {
            key: 'security',
            label: 'Security',
            children: selected.securityJapaneseName,
          },
          {
            key: 'state',
            label: 'State',
            children: <Tag>{traderState(selected)}</Tag>,
          },
          {
            key: 'routing',
            label: 'Routing',
            children: traderRouting(selected, currentUserId),
          },
        ]}
      />
      <Typography.Text type="secondary">Ownership / routing</Typography.Text>
      <Space wrap>
        <Popconfirm
          title={`Pick up Case ${selected.caseId} assigned to another trader?`}
          disabled={!pickUpRequiresConfirmation}
          onConfirm={runPickUp}
        >
          <Button
            size="small"
            disabled={!open || selected.owned || isMutating}
            onClick={() => {
              if (!pickUpRequiresConfirmation) runPickUp()
            }}
          >
            Pick Up
          </Button>
        </Popconfirm>
        <Button
          size="small"
          disabled={!open || !selected.owned || !mine || isMutating}
          onClick={() =>
            void onRun(() => onRelease(selected), patchOwnership, selected)
          }
        >
          Release
        </Button>
        <Select
          size="small"
          aria-label="Assign to trader"
          placeholder="Assign"
          value={targetTraderId}
          onChange={setTargetTraderId}
          options={traders.map((user) => ({
            value: user.userId,
            label: user.name,
          }))}
          disabled={!open || selected.owned || isMutating}
        />
        <Button
          size="small"
          disabled={!targetTraderId || !open || selected.owned || isMutating}
          onClick={() =>
            targetTraderId &&
            void onRun(
              () => onAssign(selected, targetTraderId),
              patchOwnership,
              selected,
            )
          }
        >
          Assign
        </Button>
        <Popconfirm
          title={`Take over Case ${selected.caseId}?`}
          onConfirm={() =>
            void onRun(() => onTakeOver(selected), patchOwnership, selected)
          }
        >
          <Button
            size="small"
            danger
            disabled={!open || !selected.owned || mine || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
      </Space>
      <Typography.Text type="secondary">Quote</Typography.Text>
      <Space wrap>
        <Select
          size="small"
          aria-label="Quote mode"
          value={selected.workingQuoteMode}
          disabled={!canEditQuote(selected, currentUserId) || isMutating}
          options={['Calculated', 'Manual'].map((value) => ({
            value,
            label: value,
          }))}
          onChange={(mode) =>
            void onRun(
              () => onChangeMode(selected, mode),
              patchWorkingQuote,
              selected,
            )
          }
        />
        <Button
          size="small"
          disabled={
            !onWithdraw ||
            selected.rfqStatus === 'Presented' ||
            !quoted ||
            !mine ||
            !selected.owned ||
            isMutating
          }
          onClick={() =>
            onWithdraw &&
            void onRun(() => onWithdraw(selected), patchLifecycle, selected)
          }
        >
          Withdraw
        </Button>
      </Space>
      {owner && (
        <>
          <Typography.Text type="secondary">
            Contact Owner lifecycle
          </Typography.Text>
          <Space wrap>
            <Button
              size="small"
              disabled={
                !onPresent || selected.rfqStatus !== 'Active' || !quoted
              }
              onClick={() =>
                onPresent &&
                void onRun(
                  () => onPresent(selected),
                  patchPresentation,
                  selected,
                )
              }
            >
              Present
            </Button>
            <Button
              size="small"
              disabled={!onUnpresent || selected.rfqStatus !== 'Presented'}
              onClick={() =>
                onUnpresent &&
                void onRun(
                  () => onUnpresent(selected),
                  patchPresentation,
                  selected,
                )
              }
            >
              Unpresent
            </Button>
            {(['Hit', 'Away'] as const).map((outcome) => (
              <Popconfirm
                key={outcome}
                title={`${outcome} Case ${selected.caseId}?`}
                onConfirm={() =>
                  void onRun(
                    () => onClose(selected, outcome),
                    undefined,
                    selected,
                  )
                }
              >
                <Button size="small" disabled={!open || !quoted}>
                  {outcome}
                </Button>
              </Popconfirm>
            ))}
            <Popconfirm
              title={`Cancel Case ${selected.caseId}?`}
              onConfirm={() =>
                onCancel &&
                void onRun(() => onCancel(selected), patchLifecycle, selected)
              }
            >
              <Button size="small" danger disabled={!onCancel || !open}>
                Cancel
              </Button>
            </Popconfirm>
            <Button
              size="small"
              disabled={!onReopen || selected.rfqStatus !== 'Cancelled'}
              onClick={() =>
                onReopen &&
                void onRun(() => onReopen(selected), patchLifecycle, selected)
              }
            >
              Reopen
            </Button>
            <Popconfirm
              title="Correct closed outcome?"
              onConfirm={() => {
                const reason = window.prompt('Correction Reason')
                if (!reason?.trim()) return
                void onRun(
                  () =>
                    onCorrectOutcome(
                      selected,
                      selected.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                      reason.trim(),
                    ),
                  undefined,
                  selected,
                )
              }}
            >
              <Button
                size="small"
                disabled={!['Hit', 'Away'].includes(selected.rfqStatus)}
              >
                Correct
              </Button>
            </Popconfirm>
          </Space>
          <Space.Compact block>
            <Select
              size="small"
              aria-label="Contact Owner"
              placeholder="Contact Owner"
              value={targetContactOwnerId}
              onChange={setTargetContactOwnerId}
              options={users
                .filter((user) => user.userId !== selected.contactOwnerId)
                .map((user) => ({ value: user.userId, label: user.name }))}
            />
            <Button
              size="small"
              disabled={!targetContactOwnerId}
              onClick={() =>
                targetContactOwnerId &&
                void onRun(
                  () => onChangeContactOwner(selected, targetContactOwnerId),
                  patchContactOwner,
                  selected,
                )
              }
            >
              Change
            </Button>
          </Space.Compact>
        </>
      )}
      {multi && (
        <>
          <Typography.Text type="secondary">
            Selected ({selectedRows.length})
          </Typography.Text>
          <Space wrap>
            <Popconfirm
              title={`Pick up ${bulkPickRows.length} selected RFQs, including ${otherAssignedBulkPickCount} assigned to another trader?`}
              disabled={otherAssignedBulkPickCount === 0}
              onConfirm={runBulkPick}
            >
              <Button
                size="small"
                disabled={!bulkPickRows.length || isMutating}
                onClick={() => {
                  if (otherAssignedBulkPickCount === 0) runBulkPick()
                }}
              >
                Pick
              </Button>
            </Popconfirm>
            <Button
              size="small"
              onClick={() =>
                void onBulk('Bulk Release', 'release', selectedRows)
              }
            >
              Release
            </Button>
            <Button
              size="small"
              disabled={!targetTraderId}
              onClick={() =>
                void onBulk(
                  'Bulk Assign',
                  'assign',
                  selectedRows,
                  targetTraderId,
                )
              }
            >
              Assign
            </Button>
            <Button
              size="small"
              onClick={() =>
                void onBulk('Bulk Withdraw', 'withdraw', selectedRows)
              }
            >
              Withdraw
            </Button>
            <Popconfirm
              title={`Away ${selectedRows.length} selected Cases?`}
              onConfirm={() => void onBulk('Bulk Away', 'away', selectedRows)}
            >
              <Button size="small">Away</Button>
            </Popconfirm>
            <Popconfirm
              title={`Cancel ${selectedRows.length} selected Cases?`}
              onConfirm={() =>
                void onBulk('Bulk Cancel', 'cancel', selectedRows)
              }
            >
              <Button size="small" danger>
                Cancel
              </Button>
            </Popconfirm>
          </Space>
        </>
      )}
    </div>
  )
}
