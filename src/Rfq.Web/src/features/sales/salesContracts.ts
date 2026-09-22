import type {
  BulkItemResult,
  CreateDraftRequest,
  RfqCreationContext,
  SalesRfq,
  UpdateDraftRequest,
} from '@/services/api'
import type { SalesBulkCommand } from '@/features/sales/salesModel'

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
  create: (request: CreateDraftRequest) => Promise<void>
  update: (caseId: number, request: UpdateDraftRequest) => Promise<void>
  confirmNew: (request: CreateDraftRequest) => Promise<void>
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
  createFromExisting: (caseId: number) => Promise<void>
  correctOutcome: (
    caseId: number,
    outcome: 'Hit' | 'Away',
    expectedCurrentVersion: number,
    reason: string,
  ) => Promise<void>
}

export interface SalesAmendmentActions {
  save: (
    row: SalesRfq,
    notional: number | null,
    settlementDate: string | null,
    text: string,
  ) => Promise<void>
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
  ) => Promise<BulkItemResult[]>
}

export interface SalesGridLayoutActions {
  configJson?: string
  save?: (configJson: string) => Promise<void>
}
