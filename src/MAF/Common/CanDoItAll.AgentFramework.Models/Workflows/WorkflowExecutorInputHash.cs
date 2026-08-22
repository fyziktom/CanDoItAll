using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct WorkflowExecutorInputHash
{
    private const int Sha256HexLength = 64;

    [JsonConstructor]
    public WorkflowExecutorInputHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != Sha256HexLength || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Workflow executor input hash must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public static WorkflowExecutorInputHash Compute(WorkflowNodeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.PayloadJson))));
    }

    public override string ToString() => Value;
}
