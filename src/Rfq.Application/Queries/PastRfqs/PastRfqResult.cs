using Rfq.Domain;

namespace Rfq.Application;

public sealed record PastRfqResult(IReadOnlyList<PastRfqItem> Items, bool RequiresNarrowing);
