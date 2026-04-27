namespace CanDoItAll.SharedKernel;

public interface IClock
{
    DateTimeOffset GetUtcNow();
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}
