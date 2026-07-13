using System.Globalization;

namespace CanDoItAll.Infrastructure.Storage;

internal static class FtpMachineListEntryParser
{
    public static bool TryParse(
        string parent,
        string line,
        out RemoteBrowseTransportEntry? entry)
    {
        entry = null;
        int separator = line.IndexOf(' ');
        if (separator <= 0 || separator == line.Length - 1)
        {
            return false;
        }

        string name = line[(separator + 1)..].Trim();
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\'))
        {
            return false;
        }

        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fact in line[..separator].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = fact.Split('=', 2);
            if (parts.Length != 2 || !facts.TryAdd(parts[0], parts[1]))
            {
                return false;
            }
        }

        if (!facts.TryGetValue("type", out string? type))
        {
            return false;
        }

        if (type.Equals("cdir", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("pdir", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        StorageBrowseEntryKind kind;
        if (type.Equals("dir", StringComparison.OrdinalIgnoreCase))
        {
            kind = StorageBrowseEntryKind.Container;
        }
        else if (type.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            kind = StorageBrowseEntryKind.File;
        }
        else
        {
            return false;
        }

        long? size = facts.TryGetValue("size", out string? sizeValue) &&
                     long.TryParse(sizeValue, NumberStyles.None, CultureInfo.InvariantCulture, out long parsedSize)
            ? parsedSize
            : null;
        DateTimeOffset? modified = facts.TryGetValue("modify", out string? modifyValue) &&
                                   DateTimeOffset.TryParseExact(
                                       modifyValue,
                                       "yyyyMMddHHmmss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeUniversal,
                                       out DateTimeOffset parsedModified)
            ? parsedModified.ToUniversalTime()
            : null;
        entry = new RemoteBrowseTransportEntry(
            name,
            CombineRemotePath(parent, name),
            kind,
            size,
            modified);
        return true;
    }

    private static string CombineRemotePath(string parent, string name)
        => string.Join('/', new[] { parent.Trim('/'), name.Trim('/') }.Where(value => value.Length > 0));
}
