using Rfq.Domain;

namespace Rfq.Application;

public static class PostProcessOwnership
{
    public static bool IsMine(
        UserId currentUserId,
        UserId? salesId,
        UserId contactOwnerId,
        UserId assignedTraderId) =>
        salesId == currentUserId
        || contactOwnerId == currentUserId
        || assignedTraderId == currentUserId;
}
