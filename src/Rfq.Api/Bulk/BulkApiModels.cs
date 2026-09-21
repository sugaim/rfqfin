using Rfq.Application;

namespace Rfq.Api;

public enum BulkItemStatusValue { Succeeded, Skipped, Failed }
public enum BulkFailureCodeValue { VersionConflict, InvalidState, Validation, Forbidden, NotFound }

public sealed record BulkItemResponse(
    long CaseId,
    BulkItemStatusValue Status,
    BulkFailureCodeValue? Code,
    string? Message);

public static class BulkApiMapper
{
    public static BulkItemResponse ToApi(BulkItemResult value) => new(
        value.CaseId.Value,
        Enum.Parse<BulkItemStatusValue>(value.Status.ToString()),
        value.Code is null ? null : Enum.Parse<BulkFailureCodeValue>(value.Code.Value.ToString()),
        value.Message);
}
