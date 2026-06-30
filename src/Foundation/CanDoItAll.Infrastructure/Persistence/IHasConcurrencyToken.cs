namespace CanDoItAll.Infrastructure.Persistence;

public interface IHasConcurrencyToken
{
    Guid ConcurrencyToken { get; set; }
}
