using Rfq.Domain;

namespace Rfq.Application;

public sealed record CaseMemoResult(
    CaseId CaseId,
    string Memo,
    StateVersion Version);
