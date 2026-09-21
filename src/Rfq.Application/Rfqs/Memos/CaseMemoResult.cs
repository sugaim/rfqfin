using Rfq.Domain;

namespace Rfq.Application;

public sealed record CaseMemoResult(
    long CaseId,
    string Memo,
    long Version);
