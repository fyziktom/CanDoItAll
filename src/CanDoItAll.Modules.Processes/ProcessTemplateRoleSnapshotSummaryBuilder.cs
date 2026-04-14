namespace CanDoItAll.Modules.Processes;

internal static class ProcessTemplateRoleSnapshotSummaryBuilder
{
    public static string Build(ProcessTemplateRoleResource? resource)
    {
        if (resource is null)
        {
            return string.Empty;
        }

        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(resource.SeniorityBand))
        {
            detailParts.Add($"Seniority: {resource.SeniorityBand}");
        }

        if (resource.MinimumYearsInPrimaryDiscipline > 0)
        {
            detailParts.Add($"Min years primary discipline: {resource.MinimumYearsInPrimaryDiscipline}");
        }

        if (resource.MinimumYearsInSoftwareDelivery > 0)
        {
            detailParts.Add($"Min years software delivery: {resource.MinimumYearsInSoftwareDelivery}");
        }

        if (resource.DomainTags.Count > 0)
        {
            detailParts.Add($"Domain tags: {string.Join(", ", resource.DomainTags)}");
        }

        var primarySummary = FirstNonEmpty(resource.SnapshotSummary, resource.RoleTemplateSnapshotName);
        if (string.IsNullOrWhiteSpace(primarySummary))
        {
            if (detailParts.Count > 0)
            {
                return string.Join(" | ", detailParts);
            }

            if (!string.IsNullOrWhiteSpace(resource.SeniorityBand))
            {
                return Normalize(resource.SeniorityBand) + " role template";
            }

            return FirstNonEmpty(resource.Summary, "Template import");
        }

        return detailParts.Count == 0
            ? primarySummary
            : primarySummary + " | " + string.Join(" | ", detailParts);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string Normalize(string value)
    {
        var normalized = value
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var characters = new List<char>(normalized.Length + 8);
        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(normalized[index - 1]) &&
                !char.IsUpper(normalized[index - 1]))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string(characters.ToArray());
    }
}
