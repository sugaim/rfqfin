using Rfq.Domain;

namespace Rfq.Application;

public sealed record EodSummaryItem(string ContactOwnerId, int Open, int Hit, int Away);
