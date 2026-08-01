using System.Xml;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureSvgAssetValidator
{
    private const string SvgContentType = "image/svg+xml";
    private const long MaximumXmlCharacters = ProjectStructureAssetUploadLimits.MaximumFileBytes;

    public static void Validate(ProjectObjectMediaPayload? media)
    {
        if (media is null || !IsSvg(media.FileName, media.ContentType))
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(media.Base64Data.Trim());
        }
        catch (FormatException)
        {
            return;
        }

        Validate(media.FileName, bytes);
    }

    public static void Validate(string fileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumXmlCharacters,
                    MaxCharactersFromEntities = 0
                });

            reader.MoveToContent();
            if (!string.Equals(reader.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            {
                throw ProjectStructureAgentException.CreateAgentVisible(
                    400,
                    "InvalidSvgRoot",
                    $"SVG asset '{fileName}' must have an <svg> document root. Correct the file and retry asset creation.",
                    canRetryWithCorrectedInput: true,
                    diagnosticDetails: new { fileName });
            }

            while (reader.Read())
            {
                // Reading to EOF verifies the complete XML document, not only its root element.
            }
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "InvalidSvgXml",
                $"SVG asset '{fileName}' is not well-formed XML at line {exception.LineNumber}, position {exception.LinePosition}. Escape XML text such as '&' as '&amp;' and correct the file before retrying asset creation.",
                canRetryWithCorrectedInput: true,
                diagnosticDetails: new
                {
                    fileName,
                    exception.LineNumber,
                    exception.LinePosition
                });
        }
    }

    private static bool IsSvg(string? fileName, string? contentType)
    {
        var normalizedContentType = contentType?
            .Split(';', 2, StringSplitOptions.TrimEntries)[0];
        return string.Equals(normalizedContentType, SvgContentType, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(fileName), ".svg", StringComparison.OrdinalIgnoreCase);
    }
}
