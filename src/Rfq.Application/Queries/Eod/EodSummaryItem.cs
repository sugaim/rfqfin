using Rfq.Domain;

namespace Rfq.Application;

public sealed record EodSummaryItem(UserId ContactOwnerId, int Open, int Hit, int Away);
