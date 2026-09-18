using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public sealed partial class WorkflowHttpSecretUse : IDisposable {
    public static WorkflowDisclosureOwnerId DisclosureOwner { get; } = new("workflow.http-credential");
    private readonly string headerName;
    private string[] values;
    private bool disposed;

    public WorkflowHttpSecretUse(string secret, string headerName, string headerValue,
        WorkflowProviderReadEvidence? evidence = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);
        this.headerName = headerName;
        values = new[] {
            headerValue, secret,
            JsonEncodedText.Encode(secret).ToString(), JsonEncodedText.Encode(headerValue).ToString(),
            JsonEncodedText.Encode(secret, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString(),
            JsonEncodedText.Encode(headerValue, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString()
        }.Distinct(StringComparer.Ordinal).OrderByDescending(value => value.Length).ToArray();
        Evidence = evidence;
    }

    [JsonIgnore]
    public WorkflowProviderReadEvidence? Evidence { get; }

    public string Redact(string? value, bool truncated = false) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }
        value = RedactJsonStrings(value, truncated);
        if (truncated) {
            foreach (var secret in values) {
                for (var length = Math.Min(value.Length, secret.Length - 1); length > 0; length--) {
                    if (value.AsSpan().EndsWith(secret.AsSpan(0, length), StringComparison.Ordinal)) {
                        value = string.Concat(value.AsSpan(0, value.Length - length), "[REDACTED]");
                        break;
                    }
                }
            }
        }
        foreach (var secret in values) {
            value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        }
        return SensitiveTextRedactor.Redact(value);
    }

    public string RedactHeader(string name, string value) {
        ObjectDisposedException.ThrowIf(disposed, this);
        return name.Equals(headerName, StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Api-Key", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X-Auth-Token", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                ? "[REDACTED]" : Redact(value);
    }

    public void Dispose() {
        values = [];
        disposed = true;
    }
}
