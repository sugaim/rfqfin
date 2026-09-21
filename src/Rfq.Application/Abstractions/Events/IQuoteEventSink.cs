using Rfq.Domain;

namespace Rfq.Application;

public interface IQuoteEventSink
{
    void Record(QuoteTransition transition);
}
