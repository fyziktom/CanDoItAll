using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentRecruitingEvidenceValidation
{
    internal static IReadOnlyList<string> CollectAttemptMissingEvidence(
        AppendAgentRecruitingAttemptCommand command,
        AgentRecruitingTargetResolution target)
    {
        var missing = new List<string>();
        if (!target.IsTerminal)
        {
            missing.Add("terminal-execution-target");
        }

        if (string.IsNullOrWhiteSpace(command.InputHash))
        {
            missing.Add("input-hash");
        }
        else
        {
            EnsureHash(command.InputHash, "inputHash");
        }

        if (string.IsNullOrWhiteSpace(command.OutputHash))
        {
            missing.Add("output-hash");
        }
        else
        {
            EnsureHash(command.OutputHash, "outputHash");
        }

        if (command.AutomatedEvaluation is null)
        {
            missing.Add("automated-evaluation");
        }
        else
        {
            if (!command.AutomatedEvaluation.EvaluatorAgentId.HasValue)
            {
                missing.Add("evaluator-agent-id");
            }

            if (!command.AutomatedEvaluation.ProviderProfileId.HasValue)
            {
                missing.Add("evaluator-provider-profile-id");
            }

            if (string.IsNullOrWhiteSpace(command.AutomatedEvaluation.Model))
            {
                missing.Add("evaluator-model");
            }

            if (command.AutomatedEvaluation.EvaluatedAtUtc == default)
            {
                missing.Add("evaluation-timestamp");
            }
        }

        if (!string.IsNullOrWhiteSpace(command.StructuredOutputContractKey) &&
            !string.Equals(
                command.StructuredOutputValidationStatus,
                "succeeded",
                StringComparison.OrdinalIgnoreCase))
        {
            missing.Add("successful-structured-output-validation");
        }

        return missing;
    }

    internal static AgentRecruitingAutomatedEvaluation? NormalizeEvaluation(
        AgentRecruitingAutomatedEvaluation? evaluation)
    {
        return evaluation is null
            ? null
            : evaluation with
            {
                Model = NormalizeText(evaluation.Model),
                RubricVersion = evaluation.RubricVersion.Trim(),
                Findings = (evaluation.Findings ?? [])
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };
    }

    internal static AgentRecruitingAssessmentAnalysis? NormalizeAnalysis(
        AgentRecruitingAssessmentAnalysis? analysis)
    {
        if (analysis is null)
        {
            return null;
        }

        if (!Enum.IsDefined(analysis.Classification))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.analysis-classification-invalid",
                "The assessment analysis classification is not supported.");
        }

        if (!Enum.IsDefined(analysis.ProposedNextStep))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.analysis-next-step-invalid",
                "The assessment analysis proposed next step is not supported.");
        }

        if (analysis.Confidence is < 0m or > 1m)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.analysis-confidence-invalid",
                "The assessment analysis confidence must be between 0 and 1.");
        }

        EnsureText(analysis.Summary, "analysis.summary", 4000);
        EnsureAnalysisItems(analysis.Strengths, "analysis.strengths");
        EnsureAnalysisItems(analysis.Gaps, "analysis.gaps");

        return analysis with
        {
            Summary = analysis.Summary.Trim(),
            Strengths = analysis.Strengths.Select(item => item.Trim()).ToList(),
            Gaps = analysis.Gaps.Select(item => item.Trim()).ToList()
        };
    }

    internal static void EnsureTarget(AgentRecruitingExecutionTarget? target)
    {
        if (target is null || target.Id == Guid.Empty || !Enum.IsDefined(target.Kind))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.target-invalid",
                "Exactly one supported execution target with a non-empty ID must be provided.");
        }
    }

    internal static void EnsureNonEmpty(Guid value, string field)
    {
        if (value == Guid.Empty)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.identifier-invalid",
                $"'{field}' must be a non-empty UUID.");
        }
    }

    internal static void EnsureText(string? value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.text-invalid",
                $"'{field}' must contain between 1 and {maximumLength} characters.");
        }
    }

    internal static void EnsureOptionalText(string? value, string field, int maximumLength)
    {
        if ((value?.Trim().Length ?? 0) > maximumLength)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.text-invalid",
                $"'{field}' cannot exceed {maximumLength} characters.");
        }
    }

    internal static void EnsureOptionalHash(string? value, string field)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            EnsureHash(value, field);
        }
    }

    internal static void EnsureHash(string value, string field)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sha256:".Length..];
        }

        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.hash-invalid",
                $"'{field}' must be a SHA-256 hash.");
        }
    }

    internal static string NormalizeOptionalHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sha256:".Length..];
        }

        return $"sha256:{normalized.ToLowerInvariant()}";
    }

    internal static string NormalizeText(string? value)
        => value?.Trim() ?? string.Empty;

    internal static AgentRecruitingEvidenceException Failure(
        AgentRecruitingEvidenceFailureKind kind,
        string code,
        string message)
        => new(kind, code, message);

    private static void EnsureAnalysisItems(
        IReadOnlyList<string>? items,
        string field)
    {
        if (items is null ||
            items.Count > 20 ||
            items.Any(item => string.IsNullOrWhiteSpace(item) || item.Trim().Length > 500))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.analysis-items-invalid",
                $"'{field}' must contain at most 20 non-empty items of up to 500 characters.");
        }
    }
}
