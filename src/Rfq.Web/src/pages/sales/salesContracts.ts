import type {
  CaseOperationResponse,
  CreateDraftRequest,
  InitialRfqResponse,
  AmendmentResponse,
  RfqCreationContextResponse,
  SalesRfqResponse,
  UpdateDraftRequest,
} from '@/generated/rfqApi'
import type { SalesBulkCommand } from '@/pages/sales/salesModel'

export interface UserOption {
  userId: string
  name: string
}

export interface SalesLookupActions {
  searchClients: (query: string) => void | Promise<void>
  searchSecurities: (query: string) => void | Promise<void>
  resolveDefaults: (securityId: string) => Promise<RfqCreationContextResponse>
}

export interface SalesDraftActions {
  create: (request: CreateDraftRequest) => Promise<InitialRfqResponse>
  update: (
    caseId: number,
    request: UpdateDraftRequest,
  ) => Promise<InitialRfqResponse>
  confirmNew: (request: CreateDraftRequest) => Promise<InitialRfqResponse>
  confirm: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  discard: (caseId: number, expectedVersion: number) => Promise<void>
}

export interface SalesLifecycleActions {
  present: (caseId: number, expectedCurrentVersion: number) => Promise<void>
  unpresent: (caseId: number, expectedCurrentVersion: number) => Promise<void>
  close: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
  ) => Promise<void>
  cancel: (row: SalesRfqResponse) => Promise<void>
  reopen: (row: SalesRfqResponse) => Promise<void>
  createFromExisting: (caseId: number) => Promise<InitialRfqResponse>
  correctOutcome: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
    reason: string,
  ) => Promise<void>
}

export interface SalesAmendmentActions {
  start: (row: SalesRfqResponse) => Promise<AmendmentResponse>
  save: (
    caseId: number,
    notional: number | null,
    settlementDate: string | null,
    text: string,
    expectedCurrentVersion: number,
    expectedDraftVersion: number | null,
  ) => Promise<AmendmentResponse>
  confirm: (row: SalesRfqResponse) => Promise<void>
  discard: (row: SalesRfqResponse) => Promise<void>
}

export interface SalesContactOwnerActions {
  change: (
    caseId: number,
    targetUserId: string,
    expectedCurrentVersion: number,
  ) => Promise<void>
}

export interface SalesMemoActions {
  update: (
    caseId: number,
    memo: string,
    expectedVersion: number,
  ) => Promise<void>
}

export interface SalesBulkActions {
  execute: (
    command: SalesBulkCommand,
    rows: SalesRfqResponse[],
  ) => Promise<CaseOperationResponse[]>
}

export interface SalesGridLayoutActions {
  configJson?: string
  save?: (configJson: string) => Promise<void>
}
