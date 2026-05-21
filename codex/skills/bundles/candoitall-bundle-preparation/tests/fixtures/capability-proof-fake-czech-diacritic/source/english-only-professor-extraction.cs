internal sealed class ProfessorAnchorExtraction
{
    private static readonly string[] TeachingSignals = ["remember", "learn", "because", "example", "counterexample"];

    public string SourceText { get; init; } = string.Empty;

    public bool IsTeachingTurn(string text)
        => TeachingSignals.Any(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
}
