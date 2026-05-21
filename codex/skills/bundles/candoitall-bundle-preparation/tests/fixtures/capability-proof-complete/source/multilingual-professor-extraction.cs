internal sealed class MultilingualProfessorTeachingExtractor
{
    private static readonly string[] CzechTeachingSignals = ["zapamatuj", "mylis", "priklad", "protipriklad"];

    public string SourceUtteranceOriginal { get; init; } = string.Empty;

    public static string RemoveDiacriticsForMatchingOnly(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    public string PreserveOriginalSourceUtterance() => SourceUtteranceOriginal;
}
