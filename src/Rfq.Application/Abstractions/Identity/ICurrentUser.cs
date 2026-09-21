using Rfq.Domain;

namespace Rfq.Application;

public interface ICurrentUser
{
    CurrentUser User { get; }
}
