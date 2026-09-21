using Rfq.Domain;

namespace Rfq.Application;

public sealed record PastRfqSearch(
    DateOnly? From = null, DateOnly? To = null, string? ClientId = null,
    string? SecurityId = null, string? CategoryId = null, string? ContactOwnerId = null,
    string? SalesId = null, string? AssignedTraderId = null, string? Status = null,
    long? CaseId = null);
