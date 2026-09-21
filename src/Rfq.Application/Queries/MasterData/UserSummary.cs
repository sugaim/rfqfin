using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public sealed record UserSummary(
    string UserId,
    string Name,
    IReadOnlySet<UserRole> Roles,
    string DeskId,
    int? DefaultQuoteExpiryMinutes = null);
