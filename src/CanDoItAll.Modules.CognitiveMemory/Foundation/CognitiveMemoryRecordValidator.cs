using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CognitiveMemory;

public interface ICognitiveMemoryRecordValidator
{
    Result ValidateForPersistence(CognitiveMemoryRecord record);
}

public sealed class CognitiveMemoryRecordValidator : ICognitiveMemoryRecordValidator
{
    public Result ValidateForPersistence(CognitiveMemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var errors = new List<Error>();
        if (string.IsNullOrWhiteSpace(record.Title))
        {
            errors.Add(Error.Validation("Cognitive memory records require a title.", "cognitive-memory-title-required"));
        }

        if (string.IsNullOrWhiteSpace(record.CanonicalText))
        {
            errors.Add(Error.Validation("Cognitive memory records require canonical text.", "cognitive-memory-canonical-text-required"));
        }

        if (string.IsNullOrWhiteSpace(record.AlgorithmVersion))
        {
            errors.Add(Error.Validation("Cognitive memory records require an algorithm version.", "cognitive-memory-algorithm-version-required"));
        }

        ValidateHash(record.ContentHashAlgorithm, record.ContentHash, "cognitive-memory-content-hash-invalid", errors);

        if (record.Origin == CognitiveMemoryRecordOrigin.MachineGenerated &&
            record.SourceEvidenceCount <= 0 &&
            string.IsNullOrWhiteSpace(record.GeneratedReason))
        {
            errors.Add(Error.Validation(
                "Machine-generated cognitive memory records require source evidence or an explicit generated reason.",
                "cognitive-memory-generated-evidence-required"));
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors);
    }

    private static void ValidateHash(
        CognitiveMemoryHashAlgorithm algorithm,
        string value,
        string errorCode,
        ICollection<Error> errors)
    {
        try
        {
            _ = new CognitiveMemoryHash(algorithm, value);
        }
        catch (ArgumentException exception)
        {
            errors.Add(Error.Validation(exception.Message, errorCode));
        }
    }
}

public sealed class CognitiveMemoryDefaultAccessPolicy : ICognitiveMemoryAccessPolicy
{
    public ValueTask<CognitiveMemoryAccessDecision> EvaluateAsync(
        CognitiveMemoryAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var allowed = new List<CognitiveMemoryRecordId>();
        var denied = new List<CognitiveMemoryAccessDenial>();

        foreach (var record in request.CandidateRecords)
        {
            var recordId = new CognitiveMemoryRecordId(record.Id);
            if (record.AccessLevel == CognitiveMemoryAccessLevel.Restricted &&
                !request.PolicyContext.AllowRestrictedContent)
            {
                denied.Add(new CognitiveMemoryAccessDenial(
                    recordId,
                    "restricted-content-denied",
                    "The policy context does not allow restricted cognitive memory content."));
                continue;
            }

            if (record.ProjectId.HasValue &&
                request.PolicyContext.ProjectId.HasValue &&
                record.ProjectId.Value != request.PolicyContext.ProjectId.Value)
            {
                denied.Add(new CognitiveMemoryAccessDenial(
                    recordId,
                    "project-scope-mismatch",
                    "The memory record belongs to a different project scope."));
                continue;
            }

            allowed.Add(recordId);
        }

        return ValueTask.FromResult(new CognitiveMemoryAccessDecision(allowed, denied));
    }
}
