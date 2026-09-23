import { baseApi as api } from "../services/baseApi";
const injectedRtkApi = api.injectEndpoints({
  endpoints: (build) => ({
    getActiveTraderRfqs: build.query<
      GetActiveTraderRfqsApiResponse,
      GetActiveTraderRfqsApiArg
    >({
      query: () => ({ url: `/api/worklists/trader` }),
    }),
    getActiveSalesRfqs: build.query<
      GetActiveSalesRfqsApiResponse,
      GetActiveSalesRfqsApiArg
    >({
      query: () => ({ url: `/api/worklists/sales` }),
    }),
    getSalesRecentRevisions: build.query<
      GetSalesRecentRevisionsApiResponse,
      GetSalesRecentRevisionsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/worklists/sales/recent-revisions`,
        params: {
          limit: queryArg.limit,
        },
      }),
    }),
    searchRfqs: build.query<SearchRfqsApiResponse, SearchRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/search/rfqs`,
        params: {
          CreatedFrom: queryArg.createdFrom,
          CreatedTo: queryArg.createdTo,
          ClientId: queryArg.clientId,
          SecurityId: queryArg.securityId,
          CategoryId: queryArg.categoryId,
          ContactOwnerId: queryArg.contactOwnerId,
          SalesId: queryArg.salesId,
          AssignedTraderId: queryArg.assignedTraderId,
          Status: queryArg.status,
          CaseId: queryArg.caseId,
        },
      }),
    }),
    calculateWorkingQuote: build.mutation<
      CalculateWorkingQuoteApiResponse,
      CalculateWorkingQuoteApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/working-quote/calculate`,
        method: "PUT",
        body: queryArg.calculateWorkingQuoteRequest,
      }),
    }),
    changeWorkingQuoteMode: build.mutation<
      ChangeWorkingQuoteModeApiResponse,
      ChangeWorkingQuoteModeApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/working-quote/mode`,
        method: "PUT",
        body: queryArg.changeWorkingQuoteModeRequest,
      }),
    }),
    updateManualWorkingQuote: build.mutation<
      UpdateManualWorkingQuoteApiResponse,
      UpdateManualWorkingQuoteApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/working-quote/manual`,
        method: "PUT",
        body: queryArg.updateManualWorkingQuoteRequest,
      }),
    }),
    changeContactOwners: build.mutation<
      ChangeContactOwnersApiResponse,
      ChangeContactOwnersApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/change-contact-owner`,
        method: "POST",
        body: queryArg.changeContactOwnersRequest,
      }),
    }),
    pickUpRfqs: build.mutation<PickUpRfqsApiResponse, PickUpRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/pick-up`,
        method: "POST",
        body: queryArg.pickUpRfqsRequest,
      }),
    }),
    releaseRfqs: build.mutation<ReleaseRfqsApiResponse, ReleaseRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/release`,
        method: "POST",
        body: queryArg.releaseRfqsRequest,
      }),
    }),
    assignTraders: build.mutation<
      AssignTradersApiResponse,
      AssignTradersApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/assign-trader`,
        method: "POST",
        body: queryArg.assignTradersRequest,
      }),
    }),
    takeOverRfqs: build.mutation<TakeOverRfqsApiResponse, TakeOverRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/take-over`,
        method: "POST",
        body: queryArg.takeOverRfqsRequest,
      }),
    }),
    updateSalesMemo: build.mutation<
      UpdateSalesMemoApiResponse,
      UpdateSalesMemoApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/memos/sales`,
        method: "PUT",
        body: queryArg.updateMemoRequest,
      }),
    }),
    updateTraderMemo: build.mutation<
      UpdateTraderMemoApiResponse,
      UpdateTraderMemoApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/memos/trader`,
        method: "PUT",
        body: queryArg.updateMemoRequest,
      }),
    }),
    closeHitRfq: build.mutation<CloseHitRfqApiResponse, CloseHitRfqApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/close/hit`,
        method: "POST",
        body: queryArg.closeHitRfqRequest,
      }),
    }),
    correctOutcomeToHit: build.mutation<
      CorrectOutcomeToHitApiResponse,
      CorrectOutcomeToHitApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/outcome/correct-to-hit`,
        method: "POST",
        body: queryArg.correctOutcomeToHitRequest,
      }),
    }),
    correctOutcomeToAway: build.mutation<
      CorrectOutcomeToAwayApiResponse,
      CorrectOutcomeToAwayApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/outcome/correct-to-away`,
        method: "POST",
        body: queryArg.correctOutcomeToAwayRequest,
      }),
    }),
    presentRfqs: build.mutation<PresentRfqsApiResponse, PresentRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/present`,
        method: "POST",
        body: queryArg.presentRfqsRequest,
      }),
    }),
    unpresentRfqs: build.mutation<
      UnpresentRfqsApiResponse,
      UnpresentRfqsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/unpresent`,
        method: "POST",
        body: queryArg.unpresentRfqsRequest,
      }),
    }),
    closeAwayRfqs: build.mutation<
      CloseAwayRfqsApiResponse,
      CloseAwayRfqsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/close-away`,
        method: "POST",
        body: queryArg.closeAwayRfqsRequest,
      }),
    }),
    cancelRfqs: build.mutation<CancelRfqsApiResponse, CancelRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/cancel`,
        method: "POST",
        body: queryArg.cancelRfqsRequest,
      }),
    }),
    reopenRfqs: build.mutation<ReopenRfqsApiResponse, ReopenRfqsApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/reopen`,
        method: "POST",
        body: queryArg.reopenRfqsRequest,
      }),
    }),
    createDraft: build.mutation<CreateDraftApiResponse, CreateDraftApiArg>({
      query: (queryArg) => ({
        url: `/api/rfqs/drafts`,
        method: "POST",
        body: queryArg.createDraftRequest,
      }),
    }),
    confirmNewRfq: build.mutation<
      ConfirmNewRfqApiResponse,
      ConfirmNewRfqApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/drafts/confirm`,
        method: "POST",
        body: queryArg.createDraftRequest,
      }),
    }),
    updateInitialDraft: build.mutation<
      UpdateInitialDraftApiResponse,
      UpdateInitialDraftApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/draft`,
        method: "PUT",
        body: queryArg.updateDraftRequest,
      }),
    }),
    createFromExisting: build.mutation<
      CreateFromExistingApiResponse,
      CreateFromExistingApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/create-from-existing`,
        method: "POST",
      }),
    }),
    confirmInitialDrafts: build.mutation<
      ConfirmInitialDraftsApiResponse,
      ConfirmInitialDraftsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/confirm-initial-drafts`,
        method: "POST",
        body: queryArg.confirmInitialDraftsRequest,
      }),
    }),
    discardInitialDrafts: build.mutation<
      DiscardInitialDraftsApiResponse,
      DiscardInitialDraftsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/discard-initial-drafts`,
        method: "POST",
        body: queryArg.discardInitialDraftsRequest,
      }),
    }),
    resolveRfqCreationContext: build.query<
      ResolveRfqCreationContextApiResponse,
      ResolveRfqCreationContextApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/creation-context`,
        params: {
          securityId: queryArg.securityId,
        },
      }),
    }),
    startAmendment: build.mutation<
      StartAmendmentApiResponse,
      StartAmendmentApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/amendment/start`,
        method: "POST",
        body: queryArg.startAmendmentRequest,
      }),
    }),
    saveAmendment: build.mutation<
      SaveAmendmentApiResponse,
      SaveAmendmentApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/amendment`,
        method: "PUT",
        body: queryArg.saveAmendmentRequest,
      }),
    }),
    confirmAmendments: build.mutation<
      ConfirmAmendmentsApiResponse,
      ConfirmAmendmentsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/confirm-amendments`,
        method: "POST",
        body: queryArg.confirmAmendmentsRequest,
      }),
    }),
    discardAmendments: build.mutation<
      DiscardAmendmentsApiResponse,
      DiscardAmendmentsApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/discard-amendments`,
        method: "POST",
        body: queryArg.discardAmendmentsRequest,
      }),
    }),
    getRfqQuoteHistory: build.query<
      GetRfqQuoteHistoryApiResponse,
      GetRfqQuoteHistoryApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/${encodeURIComponent(String(queryArg.caseId))}/quotes`,
      }),
    }),
    withdrawQuotes: build.mutation<
      WithdrawQuotesApiResponse,
      WithdrawQuotesApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/withdraw-quotes`,
        method: "POST",
        body: queryArg.withdrawQuotesRequest,
      }),
    }),
    confirmQuotes: build.mutation<
      ConfirmQuotesApiResponse,
      ConfirmQuotesApiArg
    >({
      query: (queryArg) => ({
        url: `/api/rfqs/confirm-quotes`,
        method: "POST",
        body: queryArg.confirmQuotesRequest,
      }),
    }),
    getAssignableTraders: build.query<
      GetAssignableTradersApiResponse,
      GetAssignableTradersApiArg
    >({
      query: () => ({ url: `/api/reference-data/assignable-traders` }),
    }),
    getContactOwnerCandidates: build.query<
      GetContactOwnerCandidatesApiResponse,
      GetContactOwnerCandidatesApiArg
    >({
      query: () => ({ url: `/api/reference-data/contact-owner-candidates` }),
    }),
    searchClients: build.query<SearchClientsApiResponse, SearchClientsApiArg>({
      query: (queryArg) => ({
        url: `/api/reference-data/clients`,
        params: {
          q: queryArg.q,
        },
      }),
    }),
    searchSecurities: build.query<
      SearchSecuritiesApiResponse,
      SearchSecuritiesApiArg
    >({
      query: (queryArg) => ({
        url: `/api/reference-data/securities`,
        params: {
          q: queryArg.q,
        },
      }),
    }),
    scratchPrice: build.mutation<ScratchPriceApiResponse, ScratchPriceApiArg>({
      query: (queryArg) => ({
        url: `/api/pricing/scratch`,
        method: "POST",
        body: queryArg.pricerRequest,
      }),
    }),
    getPostProcess: build.query<
      GetPostProcessApiResponse,
      GetPostProcessApiArg
    >({
      query: (queryArg) => ({
        url: `/api/post-process`,
        params: {
          preset: queryArg.preset,
          scope: queryArg.scope,
        },
      }),
    }),
    commitPostProcessChanges: build.mutation<
      CommitPostProcessChangesApiResponse,
      CommitPostProcessChangesApiArg
    >({
      query: (queryArg) => ({
        url: `/api/post-process/commit`,
        method: "POST",
        body: queryArg.postProcessCommitRequest,
      }),
    }),
    streamWorklistInvalidations: build.query<
      StreamWorklistInvalidationsApiResponse,
      StreamWorklistInvalidationsApiArg
    >({
      query: () => ({ url: `/api/worklists/stream` }),
    }),
    getHealth: build.query<GetHealthApiResponse, GetHealthApiArg>({
      query: () => ({ url: `/api/system/health` }),
    }),
    getReadiness: build.query<GetReadinessApiResponse, GetReadinessApiArg>({
      query: () => ({ url: `/api/system/readiness` }),
    }),
    getGridConfig: build.query<GetGridConfigApiResponse, GetGridConfigApiArg>({
      query: (queryArg) => ({
        url: `/api/me/grid-configs/${encodeURIComponent(String(queryArg.screenId))}/${encodeURIComponent(String(queryArg.configKey))}`,
      }),
    }),
    saveGridConfig: build.mutation<
      SaveGridConfigApiResponse,
      SaveGridConfigApiArg
    >({
      query: (queryArg) => ({
        url: `/api/me/grid-configs/${encodeURIComponent(String(queryArg.screenId))}/${encodeURIComponent(String(queryArg.configKey))}`,
        method: "PUT",
        body: queryArg.gridConfigRequest,
      }),
    }),
    getMe: build.query<GetMeApiResponse, GetMeApiArg>({
      query: () => ({ url: `/api/me` }),
    }),
    getQuoteExpiry: build.query<
      GetQuoteExpiryApiResponse,
      GetQuoteExpiryApiArg
    >({
      query: () => ({ url: `/api/me/settings/quote-expiry` }),
    }),
    saveQuoteExpiry: build.mutation<
      SaveQuoteExpiryApiResponse,
      SaveQuoteExpiryApiArg
    >({
      query: (queryArg) => ({
        url: `/api/me/settings/quote-expiry`,
        method: "PUT",
        body: queryArg.quoteExpiryRequest,
      }),
    }),
    getDefaultQuoteMode: build.query<
      GetDefaultQuoteModeApiResponse,
      GetDefaultQuoteModeApiArg
    >({
      query: () => ({ url: `/api/me/settings/default-quote-mode` }),
    }),
    saveDefaultQuoteMode: build.mutation<
      SaveDefaultQuoteModeApiResponse,
      SaveDefaultQuoteModeApiArg
    >({
      query: (queryArg) => ({
        url: `/api/me/settings/default-quote-mode`,
        method: "PUT",
        body: queryArg.quoteModeRequest,
      }),
    }),
    getTheme: build.query<GetThemeApiResponse, GetThemeApiArg>({
      query: () => ({ url: `/api/me/settings/theme` }),
    }),
    saveTheme: build.mutation<SaveThemeApiResponse, SaveThemeApiArg>({
      query: (queryArg) => ({
        url: `/api/me/settings/theme`,
        method: "PUT",
        body: queryArg.themeRequest,
      }),
    }),
    getBusinessDate: build.query<
      GetBusinessDateApiResponse,
      GetBusinessDateApiArg
    >({
      query: () => ({ url: `/api/business-date` }),
    }),
  }),
  overrideExisting: false,
});
export { injectedRtkApi as rfqApi };
export type GetActiveTraderRfqsApiResponse =
  /** status 200 OK */ TraderRfqResponse[];
export type GetActiveTraderRfqsApiArg = void;
export type GetActiveSalesRfqsApiResponse =
  /** status 200 OK */ SalesRfqResponse[];
export type GetActiveSalesRfqsApiArg = void;
export type GetSalesRecentRevisionsApiResponse =
  /** status 200 OK */ SalesRecentRevisionResponse[];
export type GetSalesRecentRevisionsApiArg = {
  limit?: number;
};
export type SearchRfqsApiResponse = /** status 200 OK */ RfqSearchResponse;
export type SearchRfqsApiArg = {
  createdFrom?: string;
  createdTo?: string;
  clientId?: string;
  securityId?: string;
  categoryId?: string;
  contactOwnerId?: string;
  salesId?: string;
  assignedTraderId?: string;
  status?: string;
  caseId?: number;
};
export type CalculateWorkingQuoteApiResponse =
  /** status 200 OK */ WorkingQuoteResponse;
export type CalculateWorkingQuoteApiArg = {
  caseId: number;
  calculateWorkingQuoteRequest: CalculateWorkingQuoteRequest;
};
export type ChangeWorkingQuoteModeApiResponse =
  /** status 200 OK */ WorkingQuoteResponse;
export type ChangeWorkingQuoteModeApiArg = {
  caseId: number;
  changeWorkingQuoteModeRequest: ChangeWorkingQuoteModeRequest;
};
export type UpdateManualWorkingQuoteApiResponse =
  /** status 200 OK */ WorkingQuoteResponse;
export type UpdateManualWorkingQuoteApiArg = {
  caseId: number;
  updateManualWorkingQuoteRequest: UpdateManualWorkingQuoteRequest;
};
export type ChangeContactOwnersApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ChangeContactOwnersApiArg = {
  changeContactOwnersRequest: ChangeContactOwnersRequest;
};
export type PickUpRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type PickUpRfqsApiArg = {
  pickUpRfqsRequest: PickUpRfqsRequest;
};
export type ReleaseRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ReleaseRfqsApiArg = {
  releaseRfqsRequest: ReleaseRfqsRequest;
};
export type AssignTradersApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type AssignTradersApiArg = {
  assignTradersRequest: AssignTradersRequest;
};
export type TakeOverRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type TakeOverRfqsApiArg = {
  takeOverRfqsRequest: TakeOverRfqsRequest;
};
export type UpdateSalesMemoApiResponse = /** status 200 OK */ SalesMemoResponse;
export type UpdateSalesMemoApiArg = {
  caseId: number;
  updateMemoRequest: UpdateMemoRequest;
};
export type UpdateTraderMemoApiResponse =
  /** status 200 OK */ TraderMemoResponse;
export type UpdateTraderMemoApiArg = {
  caseId: number;
  updateMemoRequest: UpdateMemoRequest;
};
export type CloseHitRfqApiResponse = /** status 200 OK */ CloseResponse;
export type CloseHitRfqApiArg = {
  caseId: number;
  closeHitRfqRequest: CloseHitRfqRequest;
};
export type CorrectOutcomeToHitApiResponse = /** status 200 OK */ CloseResponse;
export type CorrectOutcomeToHitApiArg = {
  caseId: number;
  correctOutcomeToHitRequest: CorrectOutcomeToHitRequest;
};
export type CorrectOutcomeToAwayApiResponse =
  /** status 200 OK */ CloseResponse;
export type CorrectOutcomeToAwayApiArg = {
  caseId: number;
  correctOutcomeToAwayRequest: CorrectOutcomeToAwayRequest;
};
export type PresentRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type PresentRfqsApiArg = {
  presentRfqsRequest: PresentRfqsRequest;
};
export type UnpresentRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type UnpresentRfqsApiArg = {
  unpresentRfqsRequest: UnpresentRfqsRequest;
};
export type CloseAwayRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type CloseAwayRfqsApiArg = {
  closeAwayRfqsRequest: CloseAwayRfqsRequest;
};
export type CancelRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type CancelRfqsApiArg = {
  cancelRfqsRequest: CancelRfqsRequest;
};
export type ReopenRfqsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ReopenRfqsApiArg = {
  reopenRfqsRequest: ReopenRfqsRequest;
};
export type CreateDraftApiResponse = /** status 200 OK */ InitialRfqResponse;
export type CreateDraftApiArg = {
  createDraftRequest: CreateDraftRequest;
};
export type ConfirmNewRfqApiResponse = /** status 200 OK */ InitialRfqResponse;
export type ConfirmNewRfqApiArg = {
  createDraftRequest: CreateDraftRequest;
};
export type UpdateInitialDraftApiResponse =
  /** status 200 OK */ InitialRfqResponse;
export type UpdateInitialDraftApiArg = {
  caseId: number;
  updateDraftRequest: UpdateDraftRequest;
};
export type CreateFromExistingApiResponse =
  /** status 200 OK */ InitialRfqResponse;
export type CreateFromExistingApiArg = {
  caseId: number;
};
export type ConfirmInitialDraftsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ConfirmInitialDraftsApiArg = {
  confirmInitialDraftsRequest: ConfirmInitialDraftsRequest;
};
export type DiscardInitialDraftsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type DiscardInitialDraftsApiArg = {
  discardInitialDraftsRequest: DiscardInitialDraftsRequest;
};
export type ResolveRfqCreationContextApiResponse =
  /** status 200 OK */ RfqCreationContextResponse;
export type ResolveRfqCreationContextApiArg = {
  securityId: string;
};
export type StartAmendmentApiResponse = /** status 200 OK */ AmendmentResponse;
export type StartAmendmentApiArg = {
  caseId: number;
  startAmendmentRequest: StartAmendmentRequest;
};
export type SaveAmendmentApiResponse = /** status 200 OK */ AmendmentResponse;
export type SaveAmendmentApiArg = {
  caseId: number;
  saveAmendmentRequest: SaveAmendmentRequest;
};
export type ConfirmAmendmentsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ConfirmAmendmentsApiArg = {
  confirmAmendmentsRequest: ConfirmAmendmentsRequest;
};
export type DiscardAmendmentsApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type DiscardAmendmentsApiArg = {
  discardAmendmentsRequest: DiscardAmendmentsRequest;
};
export type GetRfqQuoteHistoryApiResponse =
  /** status 200 OK */ QuoteHistoryResponse[];
export type GetRfqQuoteHistoryApiArg = {
  caseId: number;
};
export type WithdrawQuotesApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type WithdrawQuotesApiArg = {
  withdrawQuotesRequest: WithdrawQuotesRequest;
};
export type ConfirmQuotesApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type ConfirmQuotesApiArg = {
  confirmQuotesRequest: ConfirmQuotesRequest;
};
export type GetAssignableTradersApiResponse =
  /** status 200 OK */ UserCandidateResponse[];
export type GetAssignableTradersApiArg = void;
export type GetContactOwnerCandidatesApiResponse =
  /** status 200 OK */ UserCandidateResponse[];
export type GetContactOwnerCandidatesApiArg = void;
export type SearchClientsApiResponse =
  /** status 200 OK */ ClientCandidateResponse[];
export type SearchClientsApiArg = {
  q: string;
};
export type SearchSecuritiesApiResponse =
  /** status 200 OK */ SecurityCandidateResponse[];
export type SearchSecuritiesApiArg = {
  q: string;
};
export type ScratchPriceApiResponse =
  /** status 200 OK */ CalculatedQuoteResponse2;
export type ScratchPriceApiArg = {
  pricerRequest: PricerRequest;
};
export type GetPostProcessApiResponse =
  /** status 200 OK */ PostProcessItemResponse[];
export type GetPostProcessApiArg = {
  preset?: "Today" | "Unclosed";
  scope?: "Mine" | "AllPermitted";
};
export type CommitPostProcessChangesApiResponse =
  /** status 200 OK */ CaseOperationResponse[];
export type CommitPostProcessChangesApiArg = {
  postProcessCommitRequest: PostProcessCommitRequest;
};
export type StreamWorklistInvalidationsApiResponse =
  /** status 200 OK */ string;
export type StreamWorklistInvalidationsApiArg = void;
export type GetHealthApiResponse = /** status 200 OK */ HealthResponse;
export type GetHealthApiArg = void;
export type GetReadinessApiResponse = /** status 200 OK */ HealthResponse;
export type GetReadinessApiArg = void;
export type GetGridConfigApiResponse = /** status 200 OK */ GridConfigResponse;
export type GetGridConfigApiArg = {
  screenId: string;
  configKey: string;
};
export type SaveGridConfigApiResponse = /** status 200 OK */ GridConfigResponse;
export type SaveGridConfigApiArg = {
  screenId: string;
  configKey: string;
  gridConfigRequest: GridConfigRequest;
};
export type GetMeApiResponse = /** status 200 OK */ MeResponse;
export type GetMeApiArg = void;
export type GetQuoteExpiryApiResponse =
  /** status 200 OK */ QuoteExpiryResponse;
export type GetQuoteExpiryApiArg = void;
export type SaveQuoteExpiryApiResponse =
  /** status 200 OK */ QuoteExpiryResponse;
export type SaveQuoteExpiryApiArg = {
  quoteExpiryRequest: QuoteExpiryRequest;
};
export type GetDefaultQuoteModeApiResponse =
  /** status 200 OK */ QuoteModeResponse;
export type GetDefaultQuoteModeApiArg = void;
export type SaveDefaultQuoteModeApiResponse =
  /** status 200 OK */ QuoteModeResponse;
export type SaveDefaultQuoteModeApiArg = {
  quoteModeRequest: QuoteModeRequest;
};
export type GetThemeApiResponse = /** status 200 OK */ ThemeResponse;
export type GetThemeApiArg = void;
export type SaveThemeApiResponse = /** status 200 OK */ ThemeResponse;
export type SaveThemeApiArg = {
  themeRequest: ThemeRequest;
};
export type GetBusinessDateApiResponse =
  /** status 200 OK */ BusinessDateResponse;
export type GetBusinessDateApiArg = void;
export type RfqStatus =
  "Draft" | "Active" | "Presented" | "Cancelled" | "Hit" | "Away";
export type NullableOfQuoteStatus = ("Requested" | "Quoted") | null;
export type NullableOfQuoteRequestReason =
  ("Initial" | "Revised" | "Reopened" | "Expired" | "Withdrawn") | null;
export type QuoteMode = "Calculated" | "Manual";
export type CalculationDriverValue =
  | "Price"
  | "BbgYield"
  | "SimpleYield"
  | "Ysc"
  | "GSpread"
  | "Asw"
  | "ISpread"
  | "ZSpread";
export type CalculatedQuoteResponse = {
  driver: CalculationDriverValue;
  driverValue: number;
  price: number;
  bbgYield: number;
  baseSimpleYield: number;
  simpleYieldSlide: number;
  finalSimpleYield: number;
  internalYield: number;
  gSpread: number;
  asw: number;
  ysc: number;
  iSpread: number;
  zSpread: number;
} | null;
export type ManualQuoteResponse = {
  price: number | null;
  finalSimpleYield: number | null;
} | null;
export type TraderRfqResponse = {
  caseId: number;
  clientId: string;
  clientName: string;
  securityId: string;
  securityJapaneseName: string;
  securityBbgDisplay: string;
  categoryId: string;
  rfqStatus: RfqStatus;
  quoteStatus: NullableOfQuoteStatus;
  quoteRequestReason: NullableOfQuoteRequestReason;
  currentRevisionId: string;
  currentQuoteId: string | null;
  closedQuoteId: string | null;
  confirmedAt: string | null;
  expiresAt: string | null;
  quoteSeedRevisionId: string | null;
  contactOwnerId: string;
  assignedTraderId: string;
  owned: boolean;
  currentVersion: number;
  settlementDate: string | null;
  notional: number | null;
  salesAndTradingMessage: string;
  workingQuoteMode: QuoteMode;
  calculated: CalculatedQuoteResponse;
  manual: ManualQuoteResponse;
  workingQuoteVersion: number;
  traderMemo: string;
  traderMemoVersion: number;
  createdAt: string;
  stateSince: string;
};
export type ApiProblemDetails = {
  status: number;
  title: string;
  detail: string;
  code: string;
  traceId: string;
  errors?: {
    [key: string]: string[];
  };
  calculationErrorCode?: string | null;
  failureLogId?: string | null;
};
export type RevisionStatus = "Draft" | "Confirmed" | "Superseded" | "Discarded";
export type WorkingQuoteModeValue = "Calculated" | "Manual";
export type SalesConfirmedQuoteSummaryResponse = {
  quoteId: string;
  mode: WorkingQuoteModeValue;
  price: number | null;
  bbgYield: number | null;
  finalSimpleYield: number | null;
  gSpread: number | null;
  confirmedAt: string;
} | null;
export type SalesRfqResponse = {
  caseId: number;
  clientId: string;
  clientName: string;
  securityId: string;
  securityJapaneseName: string;
  securityBbgDisplay: string;
  categoryId: string;
  rfqStatus: RfqStatus;
  quoteStatus: NullableOfQuoteStatus;
  quoteRequestReason: NullableOfQuoteRequestReason;
  currentRevisionId: string;
  currentQuoteId: string | null;
  closedQuoteId: string | null;
  currentVersion: number;
  revisionStatus: RevisionStatus;
  salesId: string | null;
  contactOwnerId: string;
  assignedTraderId: string;
  settlementDate: string | null;
  standardSettlementDate: string;
  notional: number | null;
  salesAndTradingMessage: string;
  salesMemo: string;
  salesMemoVersion: number;
  version: number;
  createdAt: string;
  stateSince: string;
  confirmedQuote: SalesConfirmedQuoteSummaryResponse;
  draftRevisionId: string | null;
  draftVersion: number | null;
  draftSettlementDate: string | null;
  draftNotional: number | null;
  draftSalesAndTradingMessage: string | null;
};
export type SalesRecentRevisionKindValue = "Rfq" | "Quote";
export type SalesRecentRevisionFieldValue =
  | "Notional"
  | "Settlement"
  | "Message"
  | "Price"
  | "Yield"
  | "Simple"
  | "GSpread";
export type SalesRecentRevisionChangeResponse = {
  field: SalesRecentRevisionFieldValue;
  before: string | null;
  after: string | null;
};
export type SalesRecentRevisionResponse = {
  kind: SalesRecentRevisionKindValue;
  occurredAt: string;
  caseId: number;
  clientId: string;
  clientName: string;
  securityId: string;
  securityName: string;
  changes: SalesRecentRevisionChangeResponse[];
};
export type RfqSearchItemResponse = {
  caseId: number;
  createdAt: string;
  clientId: string;
  clientName: string;
  securityId: string;
  securityName: string;
  categoryId: string;
  status: RfqStatus;
  quoteStatus: NullableOfQuoteStatus;
  contactOwnerId: string;
  salesId: string | null;
  assignedTraderId: string;
  notional: number | null;
  settlementDate: string | null;
  price: number | null;
  finalSimpleYield: number | null;
  ysc: number | null;
};
export type RfqSearchResponse = {
  items: RfqSearchItemResponse[];
  requiresNarrowing: boolean;
};
export type WorkingQuoteResponse = {
  caseId: number;
  revisionId: string;
  mode: QuoteMode;
  calculated: CalculatedQuoteResponse;
  manual: ManualQuoteResponse;
  version: number;
  currentVersion: number;
};
export type CalculateWorkingQuoteRequest = {
  driver: CalculationDriverValue;
  value: number;
  simpleYieldSlide: number;
  expectedCurrentVersion: number;
  expectedWorkingQuoteVersion: number;
};
export type ChangeWorkingQuoteModeRequest = {
  mode: QuoteMode;
  expectedCurrentVersion: number;
  expectedWorkingQuoteVersion: number;
};
export type UpdateManualWorkingQuoteRequest = {
  price: number | null;
  finalSimpleYield: number | null;
  expectedCurrentVersion: number;
  expectedWorkingQuoteVersion: number;
};
export type CaseOperationStatus = "Applied" | "NoChange" | "Failed";
export type NullableOfCaseOperationFailureCode =
  | (
      | "VersionConflict"
      | "InvalidState"
      | "Validation"
      | "Forbidden"
      | "NotFound"
    )
  | null;
export type CaseOperationResponse = {
  caseId: number;
  status: CaseOperationStatus;
  failureCode: NullableOfCaseOperationFailureCode;
  message: string | null;
};
export type ChangeContactOwnerItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
};
export type ChangeContactOwnersRequest = {
  targetContactOwnerId: string;
  confirmed: boolean;
  items: ChangeContactOwnerItemRequest[];
};
export type OwnershipItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
};
export type PickUpRfqsRequest = {
  confirmed: boolean;
  items: OwnershipItemRequest[];
};
export type ReleaseRfqsRequest = {
  items: OwnershipItemRequest[];
};
export type AssignTradersRequest = {
  targetTraderId: string;
  items: OwnershipItemRequest[];
};
export type TakeOverRfqsRequest = {
  confirmed: boolean;
  items: OwnershipItemRequest[];
};
export type SalesMemoResponse = {
  caseId: number;
  memo: string;
  version: number;
};
export type UpdateMemoRequest = {
  memo: string | null;
  expectedMemoVersion: number;
};
export type TraderMemoResponse = {
  caseId: number;
  memo: string;
  version: number;
};
export type CloseResponse = {
  caseId: number;
  rfqStatus: RfqStatus;
  closedQuoteId: string;
  owned: boolean;
  currentVersion: number;
};
export type CloseHitRfqRequest = {
  expectedCurrentVersion: number;
};
export type CorrectOutcomeToHitRequest = {
  reason: string;
  expectedCurrentVersion: number;
};
export type CorrectOutcomeToAwayRequest = {
  reason: string;
  expectedCurrentVersion: number;
};
export type CaseVersionItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
};
export type PresentRfqsRequest = {
  items: CaseVersionItemRequest[];
};
export type UnpresentRfqsRequest = {
  items: CaseVersionItemRequest[];
};
export type CloseAwayRfqsRequest = {
  items: CaseVersionItemRequest[];
};
export type CancelRfqsRequest = {
  items: CaseVersionItemRequest[];
};
export type ReopenRfqsRequest = {
  items: CaseVersionItemRequest[];
};
export type InitialRfqResponse = {
  caseId: number;
  revisionId: string;
  rfqStatus: RfqStatus;
  revisionStatus: RevisionStatus;
  quoteStatus: NullableOfQuoteStatus;
  quoteRequestReason: NullableOfQuoteRequestReason;
  categoryId: string;
  contactOwnerId: string;
  assignedTraderId: string;
  notional: number | null;
  settlementDate: string;
  standardSettlementDate: string;
  salesAndTradingMessage: string;
  version: number;
  createdAt: string;
};
export type CreateDraftRequest = {
  clientId: string;
  securityId: string;
  notional: number | null;
  settlementDate: string | null;
  standardSettlementDate: string | null;
  salesAndTradingMessage: string;
  assignedTraderId: string;
};
export type UpdateDraftRequest = {
  notional: number | null;
  settlementDate: string | null;
  standardSettlementDate: string | null;
  salesAndTradingMessage: string;
  assignedTraderId: string;
  expectedCurrentVersion: number;
};
export type ConfirmDraftItemRequest = {
  caseId: number;
  notional: number | null;
  settlementDate: string | null;
  standardSettlementDate: string | null;
  salesAndTradingMessage: string;
  assignedTraderId: string;
  expectedCurrentVersion: number;
};
export type ConfirmInitialDraftsRequest = {
  items: ConfirmDraftItemRequest[];
};
export type DiscardDraftItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
};
export type DiscardInitialDraftsRequest = {
  items: DiscardDraftItemRequest[];
};
export type RfqCreationContextResponse = {
  categoryId: string;
  categoryName: string;
  defaultAssignedTraderId: string;
  defaultAssignedTraderName: string;
  standardSettlementDate: string;
};
export type AmendmentResponse = {
  caseId: number;
  currentRevisionId: string;
  draftRevisionId: string | null;
  currentVersion: number;
  draftVersion: number | null;
  draftNotional: number | null;
  draftSettlementDate: string | null;
  draftSalesAndTradingMessage: string | null;
  rfqStatus: RfqStatus;
  quoteStatus: NullableOfQuoteStatus;
  quoteRequestReason: NullableOfQuoteRequestReason;
};
export type StartAmendmentRequest = {
  expectedCurrentVersion: number;
};
export type SaveAmendmentRequest = {
  notional: number | null;
  settlementDate: string | null;
  salesAndTradingMessage: string | null;
  expectedCurrentVersion: number;
  expectedDraftVersion: number | null;
};
export type AmendmentActionItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
  expectedDraftVersion: number;
};
export type ConfirmAmendmentsRequest = {
  items: AmendmentActionItemRequest[];
};
export type DiscardAmendmentsRequest = {
  items: AmendmentActionItemRequest[];
};
export type QuoteRequestReason =
  "Initial" | "Revised" | "Reopened" | "Expired" | "Withdrawn";
export type QuoteHistoryResponse = {
  quoteId: string;
  revisionId: string;
  mode: QuoteMode;
  confirmedAt: string;
  expiresAt: string | null;
  requestReason: QuoteRequestReason;
};
export type WithdrawQuoteItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
};
export type WithdrawQuotesRequest = {
  items: WithdrawQuoteItemRequest[];
};
export type NullableOfQuoteExpiryType = ("None" | "After") | null;
export type QuoteExpiryRequest = {
  type: NullableOfQuoteExpiryType;
  minutes: number | null;
};
export type ConfirmQuoteItemRequest = {
  caseId: number;
  expiry: QuoteExpiryRequest;
  expectedCurrentVersion: number;
  expectedWorkingQuoteVersion: number;
};
export type ConfirmQuotesRequest = {
  items: ConfirmQuoteItemRequest[];
};
export type UserCandidateResponse = {
  userId: string;
  name: string;
};
export type ClientCandidateResponse = {
  clientId: string;
  code: string;
  name: string;
};
export type SecurityCandidateResponse = {
  securityId: string;
  japaneseName: string;
  bbgDisplay: string;
  internalCode: string;
  isin: string;
  categoryId: string;
  categoryName: string;
};
export type CalculatedQuoteResponse2 = {
  driver: CalculationDriverValue;
  driverValue: number;
  price: number;
  bbgYield: number;
  baseSimpleYield: number;
  simpleYieldSlide: number;
  finalSimpleYield: number;
  internalYield: number;
  gSpread: number;
  asw: number;
  ysc: number;
  iSpread: number;
  zSpread: number;
};
export type PricerRequest = {
  securityId: string;
  settlementDate: string;
  driver: CalculationDriverValue;
  value: number;
  simpleYieldSlide: number;
};
export type PostProcessItemResponse = {
  caseId: number;
  createdAt: string;
  createdBusinessDate: string;
  clientId: string;
  clientName: string;
  securityId: string;
  securityName: string;
  securityBbgDisplay: string;
  notional: number | null;
  settlementDate: string | null;
  contactOwnerId: string;
  salesId: string | null;
  assignedTraderId: string;
  rfqStatus: RfqStatus;
  currentVersion: number;
  salesAndTradingMessage: string;
  myMemo: string;
  myMemoVersion: number;
  price: number | null;
  finalSimpleYield: number | null;
  yield: number | null;
  ysc: number | null;
  gSpread: number | null;
  closedBusinessDate: string | null;
  lastCorrectionReason: string | null;
  lastChangedBy: string | null;
  lastChangedAt: string | null;
};
export type PostProcessLifecycleChangeValue =
  "Hit" | "Away" | "Cancel" | "CorrectToHit" | "CorrectToAway";
export type PostProcessLifecycleChangeRequest = {
  type: PostProcessLifecycleChangeValue;
  correctionReason: string | null;
} | null;
export type PostProcessMemoChangeRequest = {
  expectedMemoVersion: number;
  value: string | null;
} | null;
export type PostProcessCommitItemRequest = {
  caseId: number;
  expectedCurrentVersion: number;
  lifecycleChange: PostProcessLifecycleChangeRequest;
  memoChange: PostProcessMemoChangeRequest;
};
export type PostProcessCommitRequest = {
  items: PostProcessCommitItemRequest[];
};
export type HealthResponse = {
  status: string;
  readModelAvailable: boolean;
  runtimeBusinessDate: string | null;
  snapshotBusinessDate: string | null;
  currentGeneration: number;
  publishedGeneration: number;
  lastSuccessfulRefreshAt: string | null;
  lastFailure: string | null;
};
export type JsonElement = unknown;
export type GridConfigResponse = {
  screenId: string;
  configKey: string;
  version: number;
  config: JsonElement;
  updatedAt: string;
};
export type GridConfigRequest = {
  version: number;
  config: JsonElement;
};
export type UserRoleValue = "Sales" | "Trader";
export type MeResponse = {
  userId: string;
  roles: UserRoleValue[];
  deskId: string;
};
export type QuoteExpiryType = "None" | "After";
export type QuoteExpiryResponse = {
  type: QuoteExpiryType;
  minutes: number | null;
};
export type QuoteModeResponse = {
  mode: QuoteMode;
};
export type QuoteModeRequest = {
  mode: QuoteMode;
};
export type ThemeMode = "Light" | "Dark";
export type ThemeResponse = {
  mode: ThemeMode;
};
export type NullableOfThemeMode = ("Light" | "Dark") | null;
export type ThemeRequest = {
  mode: NullableOfThemeMode;
};
export type BusinessDateResponse = {
  date: string;
};
export const {
  useGetActiveTraderRfqsQuery,
  useLazyGetActiveTraderRfqsQuery,
  useGetActiveSalesRfqsQuery,
  useLazyGetActiveSalesRfqsQuery,
  useGetSalesRecentRevisionsQuery,
  useLazyGetSalesRecentRevisionsQuery,
  useSearchRfqsQuery,
  useLazySearchRfqsQuery,
  useCalculateWorkingQuoteMutation,
  useChangeWorkingQuoteModeMutation,
  useUpdateManualWorkingQuoteMutation,
  useChangeContactOwnersMutation,
  usePickUpRfqsMutation,
  useReleaseRfqsMutation,
  useAssignTradersMutation,
  useTakeOverRfqsMutation,
  useUpdateSalesMemoMutation,
  useUpdateTraderMemoMutation,
  useCloseHitRfqMutation,
  useCorrectOutcomeToHitMutation,
  useCorrectOutcomeToAwayMutation,
  usePresentRfqsMutation,
  useUnpresentRfqsMutation,
  useCloseAwayRfqsMutation,
  useCancelRfqsMutation,
  useReopenRfqsMutation,
  useCreateDraftMutation,
  useConfirmNewRfqMutation,
  useUpdateInitialDraftMutation,
  useCreateFromExistingMutation,
  useConfirmInitialDraftsMutation,
  useDiscardInitialDraftsMutation,
  useResolveRfqCreationContextQuery,
  useLazyResolveRfqCreationContextQuery,
  useStartAmendmentMutation,
  useSaveAmendmentMutation,
  useConfirmAmendmentsMutation,
  useDiscardAmendmentsMutation,
  useGetRfqQuoteHistoryQuery,
  useLazyGetRfqQuoteHistoryQuery,
  useWithdrawQuotesMutation,
  useConfirmQuotesMutation,
  useGetAssignableTradersQuery,
  useLazyGetAssignableTradersQuery,
  useGetContactOwnerCandidatesQuery,
  useLazyGetContactOwnerCandidatesQuery,
  useSearchClientsQuery,
  useLazySearchClientsQuery,
  useSearchSecuritiesQuery,
  useLazySearchSecuritiesQuery,
  useScratchPriceMutation,
  useGetPostProcessQuery,
  useLazyGetPostProcessQuery,
  useCommitPostProcessChangesMutation,
  useStreamWorklistInvalidationsQuery,
  useLazyStreamWorklistInvalidationsQuery,
  useGetHealthQuery,
  useLazyGetHealthQuery,
  useGetReadinessQuery,
  useLazyGetReadinessQuery,
  useGetGridConfigQuery,
  useLazyGetGridConfigQuery,
  useSaveGridConfigMutation,
  useGetMeQuery,
  useLazyGetMeQuery,
  useGetQuoteExpiryQuery,
  useLazyGetQuoteExpiryQuery,
  useSaveQuoteExpiryMutation,
  useGetDefaultQuoteModeQuery,
  useLazyGetDefaultQuoteModeQuery,
  useSaveDefaultQuoteModeMutation,
  useGetThemeQuery,
  useLazyGetThemeQuery,
  useSaveThemeMutation,
  useGetBusinessDateQuery,
  useLazyGetBusinessDateQuery,
} = injectedRtkApi;
