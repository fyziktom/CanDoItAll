namespace CanDoItAll.Manager;

public sealed class ManagerSessionService
{
    public string SessionToken { get; } = Convert.ToHexString(Guid.NewGuid().ToByteArray());

    public bool IsAuthorized(HttpRequest request)
        => string.Equals(request.Headers["X-CanDoItAll-Manager-Session"], SessionToken, StringComparison.Ordinal);
}
