using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public sealed record SecuritySearchResult(
    string SecurityId,
    string JapaneseName,
    string BbgDisplay,
    string InternalCode,
    string Isin,
    string CategoryId,
    string CategoryName);
