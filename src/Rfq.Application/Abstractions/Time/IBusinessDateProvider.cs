namespace Rfq.Application;

public interface IBusinessDateProvider
{
    Task<DateOnly> GetCurrentAsync(CancellationToken cancellationToken = default);
}
