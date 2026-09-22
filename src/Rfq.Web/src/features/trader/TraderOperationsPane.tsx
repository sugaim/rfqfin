import type { ReactElement } from 'react'
import { Descriptions, Empty, Tag } from 'antd'
import type { TraderRfq } from '@/services/api'
import { traderRouting, traderState } from '@/features/trader/traderModel'
import {
  BulkActions,
  ContactOwnerActions,
  OwnershipActions,
  QuoteActions,
  type TraderBulkRunner,
  type TraderContactOwnerActions,
  type TraderLifecycleActions,
  type TraderOperationRunner,
  type TraderOwnershipActions,
  type TraderQuoteActions,
} from '@/features/trader/TraderOperationSections'

type UserOption = { userId: string; name: string }

interface TraderOperationsPaneProps {
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
  run: TraderOperationRunner
  runBulk: TraderBulkRunner
  ownership: TraderOwnershipActions
  quote: TraderQuoteActions
  lifecycle: TraderLifecycleActions
  contactOwner: TraderContactOwnerActions
}

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
  run,
  runBulk,
  ownership,
  quote,
  lifecycle,
  contactOwner,
}: TraderOperationsPaneProps): ReactElement {
  if (!selected)
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description="Select an Active RFQ"
      />
    )

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
      <OwnershipActions
        row={selected}
        currentUserId={currentUserId}
        traders={traders}
        isMutating={isMutating}
        targetTraderId={targetTraderId}
        onTargetTraderChange={setTargetTraderId}
        run={run}
        actions={ownership}
      />
      <QuoteActions
        row={selected}
        currentUserId={currentUserId}
        isMutating={isMutating}
        run={run}
        actions={quote}
      />
      {selected.contactOwnerId === currentUserId && (
        <ContactOwnerActions
          row={selected}
          users={users}
          targetContactOwnerId={targetContactOwnerId}
          onTargetContactOwnerChange={setTargetContactOwnerId}
          run={run}
          lifecycle={lifecycle}
          contactOwner={contactOwner}
        />
      )}
      {selectedRows.length > 1 && (
        <BulkActions
          rows={selectedRows}
          currentUserId={currentUserId}
          isMutating={isMutating}
          targetTraderId={targetTraderId}
          runBulk={runBulk}
        />
      )}
    </div>
  )
}
