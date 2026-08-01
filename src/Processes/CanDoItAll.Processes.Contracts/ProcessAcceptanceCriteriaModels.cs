using System.Text.Json;
namespace CanDoItAll.Processes.Contracts;

public sealed class ProcessAcceptanceCriteriaMatrix
{
    public List<ProcessAcceptanceCriterion> Criteria { get; set; } = [];

    public IReadOnlyList<ProcessAcceptanceCriterion> RequiredCriteria
        => (Criteria ?? [])
            .Where(criterion =>
                criterion is not null &&
                criterion.RequiredForAcceptance &&
                criterion.Kind == ProcessAcceptanceCriterionKind.ProductAcceptance)
            .ToArray();

    public bool IsEmpty => Criteria is null || Criteria.Count == 0;
}

public sealed class ProcessAcceptanceCriterion
{
    public string Id { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> VerificationMethods { get; set; } = [];

    public bool RequiredForAcceptance { get; set; } = true;

    public ProcessAcceptanceCriterionKind Kind { get; set; } =
        ProcessAcceptanceCriterionKind.ProductAcceptance;
}

public enum ProcessAcceptanceCriterionKind
{
    ProductAcceptance,
    DeliveryPlanning
}

public static class ProcessAcceptanceCriteriaMatrixJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

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

            if (!IsValid(deserialized))
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

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
        options.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter<ProcessAcceptanceCriterionKind>(
                namingPolicy: null,
                allowIntegerValues: false));
        return options;
    }

    private static bool IsValid(ProcessAcceptanceCriteriaMatrix matrix)
    {
        if (matrix.Criteria is null)
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var criterion in matrix.Criteria)
        {
            if (criterion is null ||
                string.IsNullOrWhiteSpace(criterion.Id) ||
                string.IsNullOrWhiteSpace(criterion.Summary) ||
                criterion.VerificationMethods is null ||
                !Enum.IsDefined(criterion.Kind) ||
                !ids.Add(criterion.Id.Trim()))
            {
                return false;
            }
        }

        return true;
    }
}
