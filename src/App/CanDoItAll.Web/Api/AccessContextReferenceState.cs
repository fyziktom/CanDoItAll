using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Web.Api;

internal sealed class AccessContextReferenceState : IAccessContextReferenceAccessor
{
    public AccessContextReference? Current { get; private set; }

    public AccessContextReferenceType? CurrentType { get; private set; }

    public void Set(AccessContextReference reference)
        => Set(reference, null);

    public void Set(
        AccessContextReference reference,
        AccessContextReferenceType? type)
    {
        _ = reference.Value;
        if (type.HasValue)
        {
            _ = type.Value.Value;
        }

        if (Current is not { } current)
        {
            Current = reference;
            CurrentType = type;
            return;
        }

        if (current != reference || CurrentType != type)
        {
            throw new InvalidOperationException("A different access context reference is already bound to this request scope.");
        }
    }
}
