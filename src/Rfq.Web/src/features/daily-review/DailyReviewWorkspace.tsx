import { Alert, Card, Space, Tag, Typography } from 'antd'
import { AgGridReact } from 'ag-grid-react'
import { useOutletContext } from 'react-router'
import type { AppOutletContext } from '@/app/App'
import { useGetEodQuery, useSearchRfqsQuery } from '@/services/api'

export function DailyReviewWorkspace() {
  const { businessDate, events } = useOutletContext<AppOutletContext>()
  const eodQuery = useGetEodQuery(businessDate ?? '2026-09-21')
  const pastQuery = useSearchRfqsQuery({})

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card title="EOD Summary">
        {(eodQuery.data ?? []).map((item) => (
          <Space key={item.contactOwnerId} style={{ marginRight: 24 }}>
            <Typography.Text strong>{item.contactOwnerId}</Typography.Text>
            <Tag>Open {item.open}</Tag>
            <Tag color="green">Hit {item.hit}</Tag>
            <Tag color="orange">Away {item.away}</Tag>
          </Space>
        ))}
      </Card>
      <Card title="Past RFQ">
        {pastQuery.data?.requiresNarrowing && (
          <Alert
            type="warning"
            message="More than 20,000 results. Narrow the search."
          />
        )}
        <div className="rfq-grid">
          <AgGridReact
            rowData={pastQuery.data?.items ?? []}
            columnDefs={[
              { field: 'caseId' },
              { field: 'createdAt' },
              { field: 'clientName' },
              { field: 'securityName' },
              { field: 'status' },
              { field: 'quoteStatus' },
              { field: 'contactOwnerId' },
              { field: 'assignedTraderId' },
            ]}
            defaultColDef={{ sortable: true, filter: true, resizable: true }}
          />
        </div>
      </Card>
      <Card title="Changes">
        <Typography.Text>Pending Updates: {events.length}</Typography.Text>
        {events.slice(-10).map((event) => (
          <div key={event.eventId}>
            #{event.eventId} Case {event.caseId}: {event.type}
          </div>
        ))}
      </Card>
    </Space>
  )
}
