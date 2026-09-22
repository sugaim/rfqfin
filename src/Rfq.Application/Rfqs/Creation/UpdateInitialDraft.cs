using Rfq.Domain;

namespace Rfq.Application;

public sealed class UpdateInitialDraft(
    IRfqCaseRepository rfqCases,
    AssignedTraderValidator assignedTraderValidator,
    IRfqAuthorization authorization,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<InitialRfqResult> ExecuteAsync(
        UpdateInitialDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        RfqCase rfqCase = await GetCaseAsync(
            rfqCases,
            command.CaseId,
            cancellationToken);
        authorization.EnsureCanEditRevision(currentUser.User, rfqCase);
        UserId assignedTraderId = await assignedTraderValidator.ResolveAsync(
            command.AssignedTraderId,
            cancellationToken);
        if (command.StandardSettlementDate != rfqCase.CurrentRevision.StandardSettlementDate)
        {
            throw new RfqRequestValidationException(
                "Standard Settlement Date cannot differ from the RFQ creation context.");
        }

        rfqCase = InitialDraftTransitions.Update(
            rfqCase,
            new RevisionTerms(
                command.Notional,
                command.SettlementDate,
                command.StandardSettlementDate,
                command.SalesAndTradingMessage),
            assignedTraderId,
            command.ExpectedVersion);

        rfqCases.Update(rfqCase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return InitialRfqResult.From(rfqCase);
    }

    internal static async Task<RfqCase> GetCaseAsync(
        IRfqCaseRepository rfqCases,
        CaseId caseId,
        CancellationToken cancellationToken)
    {
        RfqCase rfqCase = await rfqCases.GetAsync(caseId, cancellationToken)
            ?? throw new RfqNotFoundException($"RFQ Case '{caseId}' was not found.");
        return rfqCase;
    }
}
