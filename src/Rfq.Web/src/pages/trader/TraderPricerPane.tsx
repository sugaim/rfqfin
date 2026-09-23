import type { Dispatch, ReactElement, SetStateAction } from 'react'
import {
  Alert,
  Button,
  Input,
  InputNumber,
  Select,
  Space,
  Typography,
} from 'antd'
import type {
  CalculatedQuoteResponse2,
  TraderRfqResponse,
} from '@/generated/rfqApi'
import {
  sameSourceTerms,
  type PricerProvenance,
} from '@/pages/trader/traderModel'

export type ScratchState = {
  securityId: string
  notional: number | null
  settlementDate: string
  driver: CalculatedQuoteResponse2['driver']
  value: number
  slide: number
}

const million = 1_000_000
const driverOptions: {
  value: CalculatedQuoteResponse2['driver']
  label: string
}[] = [
  { value: 'Price', label: 'Price' },
  { value: 'BbgYield', label: 'BBG Yield' },
  { value: 'SimpleYield', label: 'Simple Yield' },
  { value: 'Ysc', label: 'YSC' },
  { value: 'GSpread', label: 'G-Spread' },
  { value: 'Asw', label: 'ASW' },
  { value: 'ISpread', label: 'I-Spread' },
  { value: 'ZSpread', label: 'Z-Spread' },
]

interface TraderPricerPaneProps {
  selected?: TraderRfqResponse
  scratch: ScratchState
  setScratch: Dispatch<SetStateAction<ScratchState>>
  setScratchIdentity: (
    key: 'securityId' | 'notional' | 'settlementDate',
    value: string | number | null,
  ) => void
  result: CalculatedQuoteResponse2 | null
  provenance: PricerProvenance | null
  sourceRow?: TraderRfqResponse
  canApply: boolean
  busy: boolean
  onLoad: () => void
  onCalculate: () => void
  onApply: () => void
  onClear: () => void
}

export function TraderPricerPane({
  selected,
  scratch,
  setScratch,
  setScratchIdentity,
  result,
  provenance,
  sourceRow,
  canApply,
  busy,
  onLoad,
  onCalculate,
  onApply,
  onClear,
}: TraderPricerPaneProps): ReactElement {
  return (
    <div className="trader-pricer">
      <Space>
        <Button size="small" disabled={!selected} onClick={onLoad}>
          Load Selected RFQ
        </Button>
        <Button size="small" onClick={onClear}>
          Clear
        </Button>
      </Space>
      <Typography.Text type={provenance ? 'success' : 'secondary'}>
        {provenance ? `Source #${provenance.sourceCaseId}` : 'Detached scratch'}
      </Typography.Text>
      {provenance && sourceRow && !sameSourceTerms(sourceRow, provenance) && (
        <Alert
          type="warning"
          showIcon
          message="Source RFQ terms changed; Apply is disabled."
        />
      )}
      <Input
        size="small"
        aria-label="Pricer Security"
        placeholder="Security"
        value={scratch.securityId}
        onChange={(event) =>
          setScratchIdentity('securityId', event.target.value)
        }
      />
      <InputNumber
        size="small"
        aria-label="Pricer Notional MM"
        placeholder="Notl (MM)"
        value={scratch.notional == null ? null : scratch.notional / million}
        onChange={(value) =>
          setScratchIdentity(
            'notional',
            value == null ? null : Number(value) * million,
          )
        }
      />
      <Input
        size="small"
        aria-label="Pricer Settlement"
        type="date"
        value={scratch.settlementDate}
        onChange={(event) =>
          setScratchIdentity('settlementDate', event.target.value)
        }
      />
      <Select
        size="small"
        aria-label="Pricer Calc Type"
        value={scratch.driver}
        options={driverOptions}
        onChange={(driver) => setScratch((value) => ({ ...value, driver }))}
      />
      <InputNumber
        size="small"
        aria-label="Pricer Parameter"
        value={scratch.value}
        onChange={(value) =>
          setScratch((state) => ({ ...state, value: Number(value ?? 0) }))
        }
      />
      <InputNumber
        size="small"
        aria-label="Pricer Slide"
        value={scratch.slide}
        onChange={(value) =>
          setScratch((state) => ({ ...state, slide: Number(value ?? 0) }))
        }
      />
      <Button
        size="small"
        type="primary"
        loading={busy}
        disabled={!scratch.securityId || !scratch.settlementDate}
        onClick={onCalculate}
      >
        Calculate
      </Button>
      {result && (
        <div className="pricer-results">
          {[
            ['Px', result.price],
            ['Yld', `${result.bbgYield}%`],
            ['SY', `${result.finalSimpleYield}%`],
            ['YSC', `${result.ysc} bp`],
            ['GSpd', `${result.gSpread} bp`],
            ['ASW', `${result.asw} bp`],
            ['ISpd', `${result.iSpread} bp`],
            ['ZSpd', `${result.zSpread} bp`],
          ].map(([label, value]) => (
            <div key={label}>
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
          ))}
        </div>
      )}
      <Button size="small" disabled={!canApply} onClick={onApply}>
        {provenance ? `Apply to #${provenance.sourceCaseId}` : 'Apply'}
      </Button>
      <Typography.Paragraph type="secondary">
        Apply updates Manual Px and Final SY only. It does not Confirm.
      </Typography.Paragraph>
    </div>
  )
}
