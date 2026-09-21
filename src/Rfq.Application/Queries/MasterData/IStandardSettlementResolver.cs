using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface IStandardSettlementResolver
{
    DateOnly Resolve(SecurityId securityId, DateOnly businessDate);
}
