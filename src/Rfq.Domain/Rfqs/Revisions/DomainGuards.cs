namespace Rfq.Domain;

internal static class DomainGuards
{
    internal static void EnsureVersion(StateVersion actual, StateVersion expected, string aggregate)
    {
        if (actual != expected)
        {
            throw new StateVersionMismatchException($"The {aggregate} was changed by another user.");
        }
    }
}
