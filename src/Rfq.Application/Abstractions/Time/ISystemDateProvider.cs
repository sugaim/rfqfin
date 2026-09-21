using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface ISystemDateProvider
{
    Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default);
}
