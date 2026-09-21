using Rfq.Domain;

namespace Rfq.Application;

public interface IRfqEventSink
{
    void Record(RfqTransition transition);
}
