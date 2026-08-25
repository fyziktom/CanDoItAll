using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Web.Api;

internal sealed class AccessContextReferenceState : IAccessContextReferenceAccessor
{
    public AccessContextReference? Current { get; private set; }

    public void Set(AccessContextReference reference)
    {
        _ = reference.Value;

        if (Current is not { } current)
        {
            Current = reference;
            return;
        }

        if (current != reference)
        {
            throw new InvalidOperationException("A different access context reference is already bound to this request scope.");
        }
    }
}
