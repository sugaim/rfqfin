import type {
  CaseOperationResult,
  CreateDraftRequest,
  InitialRfqResponse,
  AmendmentResult,
  RfqCreationContext,
  SalesRfq,
  UpdateDraftRequest,
} from '@/services/api'
import type { SalesBulkCommand } from '@/pages/sales/salesModel'

export interface UserOption {
  userId: string
  name: string
}

export interface SalesLookupActions {
  searchClients: (query: string) => void | Promise<void>
  searchSecurities: (query: string) => void | Promise<void>
  resolveDefaults: (securityId: string) => Promise<RfqCreationContext>
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
  cancel: (row: SalesRfq) => Promise<void>
  reopen: (row: SalesRfq) => Promise<void>
  createFromExisting: (caseId: number) => Promise<InitialRfqResponse>
  correctOutcome: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
    reason: string,
  ) => Promise<void>
}

export interface SalesAmendmentActions {
  start: (row: SalesRfq) => Promise<AmendmentResult>
  save: (
    caseId: number,
    notional: number | null,
    settlementDate: string | null,
    text: string,
    expectedCurrentVersion: number,
    expectedDraftVersion: number | null,
  ) => Promise<AmendmentResult>
  confirm: (row: SalesRfq) => Promise<void>
  discard: (row: SalesRfq) => Promise<void>
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
    rows: SalesRfq[],
  ) => Promise<CaseOperationResult[]>
}

export interface SalesGridLayoutActions {
  configJson?: string
  save?: (configJson: string) => Promise<void>
}
