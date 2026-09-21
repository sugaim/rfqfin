using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public sealed record ClientSearchResult(
    string ClientId,
    string Code,
    string Name);
