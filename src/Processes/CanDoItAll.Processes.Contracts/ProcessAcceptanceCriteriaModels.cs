using System.Text.Json;

namespace CanDoItAll.Processes.Contracts;

public sealed class ProcessAcceptanceCriteriaMatrix
{
    public List<ProcessAcceptanceCriterion> Criteria { get; set; } = [];

    public IReadOnlyList<ProcessAcceptanceCriterion> RequiredCriteria
        => Criteria
            .Where(criterion => criterion.RequiredForAcceptance)
            .ToArray();

    public bool IsEmpty => Criteria.Count == 0;
}

public sealed class ProcessAcceptanceCriterion
{
    public string Id { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> VerificationMethods { get; set; } = [];

    public bool RequiredForAcceptance { get; set; } = true;
}

public static class ProcessAcceptanceCriteriaMatrixJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Serialize(ProcessAcceptanceCriteriaMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return JsonSerializer.Serialize(matrix, Options);
    }

    public static bool TryDeserialize(string? value, out ProcessAcceptanceCriteriaMatrix matrix)
    {
        matrix = new ProcessAcceptanceCriteriaMatrix();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<ProcessAcceptanceCriteriaMatrix>(value, Options);
            if (deserialized is null)
            {
                return false;
            }

            matrix = deserialized;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
