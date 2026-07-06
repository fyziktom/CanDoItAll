namespace CanDoItAll.Memory.Http;

public sealed class HttpMemoryProviderOptions
{
    public const string DefaultClientName = "CanDoItAll.Memory.Http";

    public string ClientName { get; set; } = DefaultClientName;

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientName))
        {
            throw new InvalidOperationException("HTTP memory provider client name must be configured.");
        }

        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("HTTP memory provider timeout must be positive.");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("HTTP memory provider retry count cannot be negative.");
        }
    }
}
