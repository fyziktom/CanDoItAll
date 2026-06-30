using System.Reflection;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

internal static class ProjectStructureNodeDetailFactory
{
    public static IReadOnlyList<ProjectStructureDetailSection> BuildSections(ProjectStructureNode node)
    {
        List<ProjectStructureDetailSection> sections = [];

        var referenceItems = BuildReferenceItems(node);
        if (referenceItems.Count > 0)
        {
            sections.Add(new ProjectStructureDetailSection("Reference", referenceItems));
        }

        var metadataItems = BuildMetadataItems(node);
        if (metadataItems.Count > 0)
        {
            sections.Add(new ProjectStructureDetailSection("Typed details", metadataItems));
        }

        return sections;
    }

    private static IReadOnlyList<ProjectStructureDetailItem> BuildReferenceItems(ProjectStructureNode node)
    {
        List<ProjectStructureDetailItem> items = [];
        AddIfValue(items, "Artifact", node.ArtifactKind);
        AddIfValue(items, "Route", node.Route);
        AddIfValue(items, "Location", $"{Math.Round(node.X)}, {Math.Round(node.Y)}");
        AddIfValue(items, "Start", node.StartUtc?.ToLocalTime().ToString("g"));
        AddIfValue(items, "End", node.EndUtc?.ToLocalTime().ToString("g"));
        if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
            storageReference is not null)
        {
            AddIfValue(items, "Storage provider", StoragePresentation.DescribeProvider(storageReference.ProviderKind));
            AddIfValue(items, "Storage locator", StoragePresentation.DescribeLocator(storageReference.LocatorKind));
            AddIfValue(items, "Storage path", storageReference.Locator);
            AddIfValue(items, "Storage route", storageReference.Route);
        }
        return items;
    }

    private static IReadOnlyList<ProjectStructureDetailItem> BuildMetadataItems(ProjectStructureNode node)
    {
        var metadata = ResolveMetadataObject(node);
        if (metadata is null)
        {
            return [];
        }

        return metadata
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Attribute = property.GetCustomAttribute<ProjectStructurePreviewFieldAttribute>()
            })
            .Where(entry => entry.Attribute is not null)
            .OrderBy(entry => entry.Attribute!.Order)
            .ThenBy(entry => entry.Attribute!.Label, StringComparer.OrdinalIgnoreCase)
            .Select(entry => BuildDetailItem(entry.Property, entry.Attribute!, metadata))
            .Where(item => item is not null)
            .Cast<ProjectStructureDetailItem>()
            .ToList();
    }

    private static ProjectStructureDetailItem? BuildDetailItem(
        PropertyInfo property,
        ProjectStructurePreviewFieldAttribute attribute,
        object metadata)
    {
        var rawValue = property.GetValue(metadata);
        if (!TryFormatValue(rawValue, out var formattedValue))
        {
            return null;
        }

        return new ProjectStructureDetailItem(attribute.Label, formattedValue);
    }

    private static object? ResolveMetadataObject(ProjectStructureNode node)
    {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        return node.ObjectType switch
        {
            ProjectObjectType.Meeting => metadata.Meeting,
            ProjectObjectType.Recording => metadata.Recording,
            ProjectObjectType.Transcript => metadata.Transcript,
            ProjectObjectType.Participant => metadata.Participant,
            ProjectObjectType.WorkItem => metadata.WorkItem,
            ProjectObjectType.Repository => metadata.Repository,
            ProjectObjectType.File => metadata.File,
            ProjectObjectType.Script => metadata.Script,
            ProjectObjectType.Environment => metadata.Environment,
            ProjectObjectType.Infrastructure => metadata.Infrastructure,
            ProjectObjectType.Link => metadata.Link,
            _ => null
        };
    }

    private static bool TryFormatValue(object? rawValue, out string formattedValue)
    {
        formattedValue = string.Empty;
        switch (rawValue)
        {
            case null:
                return false;
            case string stringValue:
                formattedValue = stringValue.Trim();
                return !string.IsNullOrWhiteSpace(formattedValue);
            case bool boolValue:
                if (!boolValue)
                {
                    return false;
                }

                formattedValue = "Yes";
                return true;
            case Guid guidValue when guidValue != Guid.Empty:
                formattedValue = guidValue.ToString();
                return true;
            case DateTimeOffset dateTimeOffsetValue:
                formattedValue = dateTimeOffsetValue.ToLocalTime().ToString("g");
                return true;
            case int intValue when intValue > 0:
                formattedValue = intValue.ToString();
                return true;
            case decimal decimalValue when decimalValue > 0:
                formattedValue = decimalValue.ToString("0.##");
                return true;
            case IEnumerable<Guid> guidSequence:
                var linkedCount = guidSequence.Count(item => item != Guid.Empty);
                if (linkedCount == 0)
                {
                    return false;
                }

                formattedValue = linkedCount == 1
                    ? "1 linked item"
                    : $"{linkedCount} linked items";
                return true;
        }

        var type = rawValue.GetType();
        if (type.IsEnum)
        {
            if (Convert.ToInt32(rawValue) == 0)
            {
                return false;
            }

            formattedValue = HumanizeToken(rawValue.ToString());
            return !string.IsNullOrWhiteSpace(formattedValue);
        }

        formattedValue = rawValue.ToString()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(formattedValue);
    }

    private static void AddIfValue(List<ProjectStructureDetailItem> items, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            items.Add(new ProjectStructureDetailItem(label, value.Trim()));
        }
    }

    private static string HumanizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("dotnet", ".NET", StringComparison.OrdinalIgnoreCase)
            .Replace("ai", "AI", StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Replace('_', ' ');
    }
}
