using Rfq.Domain;

namespace Rfq.Application;

public sealed record RfqDefaultsResult(
    string SecurityId,
    string CategoryId,
    string CategoryName,
    string ContactOwnerId,
    string ContactOwnerName,
    string AssignedTraderId,
    string AssignedTraderName,
    DateOnly SystemDate,
    DateOnly StandardSettlementDate);
