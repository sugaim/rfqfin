import type { ReactElement } from 'react'
import { Descriptions, Empty, Tag } from 'antd'
import type { TraderRfq } from '@/services/api'
import { traderRouting, traderState } from '@/features/trader/traderModel'
import type { UserOption } from '@/features/trader/traderContracts'
import { OwnershipActions } from '@/features/trader/operations/OwnershipActions'
import { QuoteActions } from '@/features/trader/operations/QuoteActions'
import { ContactOwnerActions } from '@/features/trader/operations/ContactOwnerActions'
import { BulkActions } from '@/features/trader/operations/BulkActions'
import type { TraderOperationController } from '@/features/trader/operations/useTraderOperationIntents'

interface TraderOperationsPaneProps {
  selected?: TraderRfq
  selectedRows: TraderRfq[]
  currentUserId: string
  traders: UserOption[]
  users: UserOption[]
  isMutating: boolean
  controller: TraderOperationController
}

export function TraderOperationsPane({
  selected,
  selectedRows,
  currentUserId,
  traders,
  users,
  isMutating,
  controller,
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
        targetTraderId={controller.targetTraderId}
        onTargetTraderChange={controller.setTargetTraderId}
        intents={controller.ownership}
      />
      <QuoteActions
        row={selected}
        currentUserId={currentUserId}
        isMutating={isMutating}
        intents={controller.quote}
      />
      {selected.contactOwnerId === currentUserId && (
        <ContactOwnerActions
          row={selected}
          users={users}
          targetContactOwnerId={controller.targetContactOwnerId}
          onTargetContactOwnerChange={controller.setTargetContactOwnerId}
          lifecycle={controller.lifecycle}
          contactOwner={controller.contactOwner}
        />
      )}
      {selectedRows.length > 1 && (
        <BulkActions
          rows={selectedRows}
          currentUserId={currentUserId}
          isMutating={isMutating}
          targetTraderId={controller.targetTraderId}
          intents={controller.bulk}
        />
      )}
    </div>
  )
}
