using Rfq.Domain;

namespace Rfq.Application;

public sealed record MemoResult(
    CaseId CaseId,
    string Memo,
    StateVersion Version);
