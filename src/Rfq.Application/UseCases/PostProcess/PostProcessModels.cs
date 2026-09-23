using Rfq.Domain;

namespace Rfq.Application;

public enum PostProcessPreset
{
    Today,
    Unclosed,
}

public enum PostProcessScope
{
    Mine,
    AllPermitted,
}

public enum PostProcessLifecycleChangeKind
{
    Hit,
    Away,
    Cancel,
    CorrectToHit,
    CorrectToAway,
}

public sealed record PostProcessWorklistItem(
    CaseId CaseId,
    DateTimeOffset CreatedAt,
    DateOnly CreatedBusinessDate,
    ClientId ClientId,
    string ClientName,
    SecurityId SecurityId,
    string SecurityName,
    string SecurityBbgDisplay,
    decimal? Notional,
    DateOnly? SettlementDate,
    UserId ContactOwnerId,
    UserId? SalesId,
    UserId AssignedTraderId,
    RfqStatus RfqStatus,
    StateVersion CurrentVersion,
    string SalesAndTradingMessage,
    string MyMemo,
    StateVersion MyMemoVersion,
    decimal? Price,
    decimal? FinalSimpleYield,
    decimal? Yield,
    decimal? Ysc,
    decimal? GSpread,
    DateOnly? ClosedBusinessDate,
    string? LastCorrectionReason,
    UserId? LastChangedBy,
    DateTimeOffset? LastChangedAt);

public sealed record PostProcessLifecycleChange(
    PostProcessLifecycleChangeKind Type,
    string? CorrectionReason = null);

public sealed record PostProcessMemoChange(
    StateVersion ExpectedVersion,
    string? Value);

public sealed record PostProcessCommitItem(
    CaseId CaseId,
    StateVersion ExpectedCurrentVersion,
    PostProcessLifecycleChange? LifecycleChange,
    PostProcessMemoChange? MemoChange);
