using CanDoItAll.Memory.SourceGateway;

namespace CanDoItAll.Memory.Application;

internal static class MemorySourcePayloadClassifier
{
    public static IReadOnlyList<MemorySourcePayloadForm> Classify(MemorySourceSnapshot snapshot)
    {
        var forms = new HashSet<MemorySourcePayloadForm>();
        foreach (var item in snapshot.Items)
        {
            if (LooksLikeStructuredJson(item.Content))
            {
                forms.Add(MemorySourcePayloadForm.StructuredJsonFacts);
            }
            else if (!string.IsNullOrWhiteSpace(item.Content))
            {
                forms.Add(MemorySourcePayloadForm.TextSection);
            }

            if (item.StorageReference is not null)
            {
                forms.Add(MemorySourcePayloadForm.FileReference);
                forms.Add(MemorySourcePayloadForm.BinaryOrExternalReference);
                if (string.Equals(item.StorageReference.LocatorKind, "url", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.StorageReference.LocatorKind, "uri", StringComparison.OrdinalIgnoreCase))
                {
                    forms.Add(MemorySourcePayloadForm.LinkReference);
                }
            }

            if (item.References.Count > 0)
            {
                forms.Add(MemorySourcePayloadForm.ArtifactReference);
            }

            if (item.Links.Count > 0)
            {
                forms.Add(MemorySourcePayloadForm.LinkReference);
            }
        }

        return forms.OrderBy(form => form).ToArray();
    }

    private static bool LooksLikeStructuredJson(string value)
    {
        var normalized = value.TrimStart();
        return normalized.StartsWith('{') || normalized.StartsWith('[');
    }
}
