using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

internal static class OfficeEvidenceDiagnosticRules
{
    public static IReadOnlyList<ProcessDriverDiagnostic> Evaluate(
        IReadOnlyList<OfficeEvidenceItem> items,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office evidence item is missing an item id.", primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(item.SubjectOrTitle))
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office evidence item is missing subject or title metadata.", primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(item.SenderOrAuthor))
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office evidence item is missing sender or author metadata.", primaryEvidence));
            }

            if (item.Kind == OfficeEvidenceItemKind.EmailMessage &&
                (item.Recipients is null || item.Recipients.Count == 0))
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office email evidence item is missing recipient metadata.", primaryEvidence));
            }

            if (item.ObservedAt is null)
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office evidence item is missing observed timestamp metadata.", primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                diagnostics.Add(CreateMissingMetadataDiagnostic("Office evidence item is missing supplied text content.", primaryEvidence));
            }
        }

        return diagnostics;
    }

    private static ProcessDriverDiagnostic CreateMissingMetadataDiagnostic(
        string message,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        return OfficeEvidenceDiagnosticFactory.Create(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.InsufficientProof,
            message,
            primaryEvidence);
    }
}
