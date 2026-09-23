import type { ReactElement } from 'react'
import {
  AutoComplete,
  Button,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Spin,
} from 'antd'
import type { FormInstance } from 'antd'
import type {
  ClientCandidateResponse,
  SalesRfqResponse,
  SecurityCandidateResponse,
} from '@/generated/rfqApi'

export interface RfqFormValues {
  clientId: string
  securityId: string
  categoryName: string
  assignedTraderId: string
  settlementDate: string
  standardSettlementDate: string
  notional?: number
  salesAndTradingMessage: string
}

interface SalesRfqEditorProps {
  form: FormInstance<RfqFormValues>
  mode: 'new' | 'draft'
  selected?: SalesRfqResponse
  clients: ClientCandidateResponse[]
  securities: SecurityCandidateResponse[]
  traders: { userId: string; name: string }[]
  defaultsLoading: boolean
  isMutating: boolean
  onClientSearch: (query: string) => void | Promise<void>
  onSecuritySearch: (query: string) => void | Promise<void>
  onSecuritySelect: (securityId: string) => void | Promise<void>
  onEditStart: () => void
  onEditComplete: (
    field:
      | 'assignedTraderId'
      | 'settlementDate'
      | 'notional'
      | 'salesAndTradingMessage',
  ) => void | Promise<void>
  onSave: (confirm: boolean) => void | Promise<void>
  onDiscard: (row: SalesRfqResponse) => void | Promise<void>
}

export function SalesRfqEditor({
  form,
  mode,
  selected,
  clients,
  securities,
  traders,
  defaultsLoading,
  isMutating,
  onClientSearch,
  onSecuritySearch,
  onSecuritySelect,
  onEditStart,
  onEditComplete,
  onSave,
  onDiscard,
}: SalesRfqEditorProps): ReactElement {
  return (
    <Spin spinning={defaultsLoading}>
      <Form<RfqFormValues>
        form={form}
        layout="vertical"
        size="small"
        className="sales-form"
      >
        <Form.Item name="clientId" label="Client" rules={[{ required: true }]}>
          <AutoComplete
            disabled={mode === 'draft'}
            filterOption={false}
            onSearch={(value) => void onClientSearch(value)}
            options={clients.map((client) => ({
              value: client.clientId,
              label: `${client.name} · ${client.code}`,
            }))}
          />
        </Form.Item>
        <Form.Item
          name="securityId"
          label="Security"
          rules={[{ required: true }]}
        >
          <AutoComplete
            disabled={mode === 'draft'}
            filterOption={false}
            onSearch={(value) => void onSecuritySearch(value)}
            onSelect={(value) => void onSecuritySelect(value)}
            options={securities.map((security) => ({
              value: security.securityId,
              label: `${security.japaneseName} · ${security.bbgDisplay}`,
            }))}
          />
        </Form.Item>
        <div className="sales-form-subfields">
          <Form.Item name="categoryName" label="Category">
            <Input disabled />
          </Form.Item>
          <Form.Item name="assignedTraderId" label="Trader">
            <Select
              onFocus={onEditStart}
              onBlur={() => void onEditComplete('assignedTraderId')}
              options={traders.map((trader) => ({
                value: trader.userId,
                label: trader.name,
              }))}
            />
          </Form.Item>
        </div>
        <Form.Item name="settlementDate" label="Settle">
          <Input
            type="date"
            onFocus={onEditStart}
            onBlur={() => void onEditComplete('settlementDate')}
          />
        </Form.Item>
        <Form.Item name="standardSettlementDate" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="notional" label="Notl (MM)">
          <InputNumber
            min={0}
            precision={2}
            onFocus={onEditStart}
            onBlur={() => void onEditComplete('notional')}
          />
        </Form.Item>
        <Form.Item name="salesAndTradingMessage" label="Message">
          <Input
            onFocus={onEditStart}
            onBlur={() => void onEditComplete('salesAndTradingMessage')}
          />
        </Form.Item>
        <Space wrap>
          {mode === 'new' && (
            <Button
              size="small"
              loading={isMutating}
              onClick={() => void onSave(false)}
            >
              Save Draft
            </Button>
          )}
          <Button
            size="small"
            type="primary"
            loading={isMutating}
            onClick={() => void onSave(true)}
          >
            Confirm
          </Button>
          {mode === 'draft' && selected && (
            <Button
              size="small"
              danger
              onClick={() => void onDiscard(selected)}
            >
              Discard
            </Button>
          )}
        </Space>
      </Form>
    </Spin>
  )
}
