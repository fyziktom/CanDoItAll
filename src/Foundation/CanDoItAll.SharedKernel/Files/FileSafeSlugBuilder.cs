namespace CanDoItAll.SharedKernel;

public static class FileSafeSlugBuilder
{
    public static string Build(string input)
    {
        var slug = input.Trim().ToLowerInvariant();
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            slug = slug.Replace(character.ToString(), string.Empty, StringComparison.Ordinal);
        }

        slug = slug.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(slug)
            ? Guid.NewGuid().ToString("N")
            : slug;
    }
}
