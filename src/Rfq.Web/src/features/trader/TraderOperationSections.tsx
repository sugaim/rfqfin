import type { ReactElement } from 'react'
import { Button, Popconfirm, Select, Space, Typography } from 'antd'
import type {
  CloseRfqResult,
  ContactOwnerResult,
  LifecycleResult,
  OwnershipResult,
  PresentationResult,
  TraderRfq,
  WorkingQuoteResult,
} from '@/services/api'
import {
  canEditQuote,
  isPickUpEligible,
  patchContactOwner,
  patchLifecycle,
  patchOwnership,
  patchPresentation,
  patchWorkingQuote,
  requiresPickUpConfirmation,
  type TraderBulkCommand,
} from '@/features/trader/traderModel'

type UserOption = { userId: string; name: string }

export type TraderOperationRunner = <T>(
  action: () => Promise<T | void>,
  patch?: (row: TraderRfq, value: T) => TraderRfq,
  row?: TraderRfq,
) => Promise<T | void>

export type TraderBulkRunner = (
  label: string,
  command: TraderBulkCommand,
  rows: TraderRfq[],
  trader?: string,
) => Promise<void>

export interface TraderOwnershipActions {
  pickUp: (
    row: TraderRfq,
    confirmed: boolean,
  ) => Promise<OwnershipResult | void>
  release: (row: TraderRfq) => Promise<OwnershipResult | void>
  assign: (
    row: TraderRfq,
    targetTraderId: string,
  ) => Promise<OwnershipResult | void>
  takeOver: (row: TraderRfq) => Promise<OwnershipResult | void>
}

export interface TraderQuoteActions {
  changeMode: (
    row: TraderRfq,
    mode: 'Calculated' | 'Manual',
  ) => Promise<WorkingQuoteResult | void>
  withdraw?: (row: TraderRfq) => Promise<LifecycleResult | void>
}

export interface TraderLifecycleActions {
  present?: (row: TraderRfq) => Promise<PresentationResult | void>
  unpresent?: (row: TraderRfq) => Promise<PresentationResult | void>
  close: (
    row: TraderRfq,
    outcome: 'Hit' | 'Away',
  ) => Promise<CloseRfqResult | void>
  cancel?: (row: TraderRfq) => Promise<LifecycleResult | void>
  reopen?: (row: TraderRfq) => Promise<LifecycleResult | void>
  correctOutcome: (
    row: TraderRfq,
    outcome: 'Hit' | 'Away',
    reason: string,
  ) => Promise<CloseRfqResult | void>
}

export interface TraderContactOwnerActions {
  change: (
    row: TraderRfq,
    targetUserId: string,
  ) => Promise<ContactOwnerResult | void>
}

interface OwnershipActionsProps {
  row: TraderRfq
  currentUserId: string
  traders: UserOption[]
  isMutating: boolean
  targetTraderId?: string
  onTargetTraderChange: (value?: string) => void
  run: TraderOperationRunner
  actions: TraderOwnershipActions
}

export function OwnershipActions({
  row,
  currentUserId,
  traders,
  isMutating,
  targetTraderId,
  onTargetTraderChange,
  run,
  actions,
}: OwnershipActionsProps): ReactElement {
  const open = ['Active', 'Presented'].includes(row.rfqStatus)
  const mine = row.assignedTraderId === currentUserId
  const pickUpRequiresConfirmation = requiresPickUpConfirmation(
    row,
    currentUserId,
  )
  const pickUp = () =>
    void run(
      () => actions.pickUp(row, pickUpRequiresConfirmation),
      patchOwnership,
      row,
    )

  return (
    <>
      <Typography.Text type="secondary">Ownership / routing</Typography.Text>
      <Space wrap>
        <Popconfirm
          title={`Pick up Case ${row.caseId} assigned to another trader?`}
          disabled={!pickUpRequiresConfirmation}
          onConfirm={pickUp}
        >
          <Button
            size="small"
            disabled={!open || row.owned || isMutating}
            onClick={() => {
              if (!pickUpRequiresConfirmation) pickUp()
            }}
          >
            Pick Up
          </Button>
        </Popconfirm>
        <Button
          size="small"
          disabled={!open || !row.owned || !mine || isMutating}
          onClick={() =>
            void run(() => actions.release(row), patchOwnership, row)
          }
        >
          Release
        </Button>
        <Select
          size="small"
          aria-label="Assign to trader"
          placeholder="Assign"
          value={targetTraderId}
          onChange={onTargetTraderChange}
          options={traders.map((user) => ({
            value: user.userId,
            label: user.name,
          }))}
          disabled={!open || row.owned || isMutating}
        />
        <Button
          size="small"
          disabled={!targetTraderId || !open || row.owned || isMutating}
          onClick={() =>
            targetTraderId &&
            void run(
              () => actions.assign(row, targetTraderId),
              patchOwnership,
              row,
            )
          }
        >
          Assign
        </Button>
        <Popconfirm
          title={`Take over Case ${row.caseId}?`}
          onConfirm={() =>
            void run(() => actions.takeOver(row), patchOwnership, row)
          }
        >
          <Button
            size="small"
            danger
            disabled={!open || !row.owned || mine || isMutating}
          >
            Take Over
          </Button>
        </Popconfirm>
      </Space>
    </>
  )
}

interface QuoteActionsProps {
  row: TraderRfq
  currentUserId: string
  isMutating: boolean
  run: TraderOperationRunner
  actions: TraderQuoteActions
}

export function QuoteActions({
  row,
  currentUserId,
  isMutating,
  run,
  actions,
}: QuoteActionsProps): ReactElement {
  const mine = row.assignedTraderId === currentUserId
  const quoted = row.quoteStatus === 'Quoted'
  const withdraw = actions.withdraw

  return (
    <>
      <Typography.Text type="secondary">Quote</Typography.Text>
      <Space wrap>
        <Select
          size="small"
          aria-label="Quote mode"
          value={row.workingQuoteMode}
          disabled={!canEditQuote(row, currentUserId) || isMutating}
          options={['Calculated', 'Manual'].map((value) => ({
            value,
            label: value,
          }))}
          onChange={(mode) =>
            void run(
              () => actions.changeMode(row, mode),
              patchWorkingQuote,
              row,
            )
          }
        />
        <Button
          size="small"
          disabled={
            !withdraw ||
            row.rfqStatus === 'Presented' ||
            !quoted ||
            !mine ||
            !row.owned ||
            isMutating
          }
          onClick={() =>
            withdraw && void run(() => withdraw(row), patchLifecycle, row)
          }
        >
          Withdraw
        </Button>
      </Space>
    </>
  )
}

interface ContactOwnerActionsProps {
  row: TraderRfq
  users: UserOption[]
  targetContactOwnerId?: string
  onTargetContactOwnerChange: (value?: string) => void
  run: TraderOperationRunner
  lifecycle: TraderLifecycleActions
  contactOwner: TraderContactOwnerActions
}

export function ContactOwnerActions({
  row,
  users,
  targetContactOwnerId,
  onTargetContactOwnerChange,
  run,
  lifecycle,
  contactOwner,
}: ContactOwnerActionsProps): ReactElement {
  const open = ['Active', 'Presented'].includes(row.rfqStatus)
  const quoted = row.quoteStatus === 'Quoted'
  const { present, unpresent, cancel, reopen } = lifecycle

  return (
    <>
      <Typography.Text type="secondary">
        Contact Owner lifecycle
      </Typography.Text>
      <Space wrap>
        <Button
          size="small"
          disabled={!present || row.rfqStatus !== 'Active' || !quoted}
          onClick={() =>
            present && void run(() => present(row), patchPresentation, row)
          }
        >
          Present
        </Button>
        <Button
          size="small"
          disabled={!unpresent || row.rfqStatus !== 'Presented'}
          onClick={() =>
            unpresent && void run(() => unpresent(row), patchPresentation, row)
          }
        >
          Unpresent
        </Button>
        {(['Hit', 'Away'] as const).map((outcome) => (
          <Popconfirm
            key={outcome}
            title={`${outcome} Case ${row.caseId}?`}
            onConfirm={() =>
              void run(() => lifecycle.close(row, outcome), undefined, row)
            }
          >
            <Button size="small" disabled={!open || !quoted}>
              {outcome}
            </Button>
          </Popconfirm>
        ))}
        <Popconfirm
          title={`Cancel Case ${row.caseId}?`}
          onConfirm={() =>
            cancel && void run(() => cancel(row), patchLifecycle, row)
          }
        >
          <Button size="small" danger disabled={!cancel || !open}>
            Cancel
          </Button>
        </Popconfirm>
        <Button
          size="small"
          disabled={!reopen || row.rfqStatus !== 'Cancelled'}
          onClick={() =>
            reopen && void run(() => reopen(row), patchLifecycle, row)
          }
        >
          Reopen
        </Button>
        <Popconfirm
          title="Correct closed outcome?"
          onConfirm={() => {
            const reason = window.prompt('Correction Reason')
            if (!reason?.trim()) return

            void run(
              () =>
                lifecycle.correctOutcome(
                  row,
                  row.rfqStatus === 'Hit' ? 'Away' : 'Hit',
                  reason.trim(),
                ),
              undefined,
              row,
            )
          }}
        >
          <Button
            size="small"
            disabled={!['Hit', 'Away'].includes(row.rfqStatus)}
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
          onChange={onTargetContactOwnerChange}
          options={users
            .filter((user) => user.userId !== row.contactOwnerId)
            .map((user) => ({ value: user.userId, label: user.name }))}
        />
        <Button
          size="small"
          disabled={!targetContactOwnerId}
          onClick={() =>
            targetContactOwnerId &&
            void run(
              () => contactOwner.change(row, targetContactOwnerId),
              patchContactOwner,
              row,
            )
          }
        >
          Change
        </Button>
      </Space.Compact>
    </>
  )
}

interface BulkActionsProps {
  rows: TraderRfq[]
  currentUserId: string
  isMutating: boolean
  targetTraderId?: string
  runBulk: TraderBulkRunner
}

export function BulkActions({
  rows,
  currentUserId,
  isMutating,
  targetTraderId,
  runBulk,
}: BulkActionsProps): ReactElement {
  const pickRows = rows.filter(isPickUpEligible)
  const otherAssignedPickCount = pickRows.filter((row) =>
    requiresPickUpConfirmation(row, currentUserId),
  ).length
  const pick = () => void runBulk('Bulk Pick', 'pick', pickRows)

  return (
    <>
      <Typography.Text type="secondary">
        Selected ({rows.length})
      </Typography.Text>
      <Space wrap>
        <Popconfirm
          title={`Pick up ${pickRows.length} selected RFQs, including ${otherAssignedPickCount} assigned to another trader?`}
          disabled={otherAssignedPickCount === 0}
          onConfirm={pick}
        >
          <Button
            size="small"
            disabled={!pickRows.length || isMutating}
            onClick={() => {
              if (otherAssignedPickCount === 0) pick()
            }}
          >
            Pick
          </Button>
        </Popconfirm>
        <Button
          size="small"
          onClick={() => void runBulk('Bulk Release', 'release', rows)}
        >
          Release
        </Button>
        <Button
          size="small"
          disabled={!targetTraderId}
          onClick={() =>
            void runBulk('Bulk Assign', 'assign', rows, targetTraderId)
          }
        >
          Assign
        </Button>
        <Button
          size="small"
          onClick={() => void runBulk('Bulk Withdraw', 'withdraw', rows)}
        >
          Withdraw
        </Button>
        <Popconfirm
          title={`Away ${rows.length} selected Cases?`}
          onConfirm={() => void runBulk('Bulk Away', 'away', rows)}
        >
          <Button size="small">Away</Button>
        </Popconfirm>
        <Popconfirm
          title={`Cancel ${rows.length} selected Cases?`}
          onConfirm={() => void runBulk('Bulk Cancel', 'cancel', rows)}
        >
          <Button size="small" danger>
            Cancel
          </Button>
        </Popconfirm>
      </Space>
    </>
  )
}
