namespace CanDoItAll.SharedKernel;

public static class FileSafeSlugBuilder
{
    public static string Build(string input)
    {
        var slug = input.Trim().ToLowerInvariant().Replace(' ', '-');
        if (slug.Length == 0)
        {
            return Guid.NewGuid().ToString("N");
        }

        return PortablePhysicalFileNamePolicy.Encode(slug).PhysicalName;
    }
}
