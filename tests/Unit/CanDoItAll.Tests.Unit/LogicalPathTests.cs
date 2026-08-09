using CanDoItAll.SharedKernel;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Tests.Unit;

public sealed class LogicalPathTests
{
    [Fact]
    public void Parse_preserves_canonical_value_and_uses_ordinal_equality()
    {
        var path = LogicalPath.Parse("artifacts/run-01/output.json");

        Assert.Equal("artifacts/run-01/output.json", path.Value);
        Assert.Equal(["artifacts", "run-01", "output.json"], path.Segments);
        Assert.Equal(path, LogicalPath.Parse("artifacts/run-01/output.json"));
        Assert.NotEqual(path, LogicalPath.Parse("Artifacts/run-01/output.json"));
    }

    [Fact]
    public void ParseLegacyWindowsLogicalPath_canonicalizes_only_the_explicit_legacy_boundary()
    {
        var path = LogicalPath.ParseLegacyWindowsLogicalPath(@"managed-files\project-media\quote.pdf");

        Assert.Equal("managed-files/project-media/quote.pdf", path.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/absolute")]
    [InlineData("relative//empty")]
    [InlineData("relative/./dot")]
    [InlineData("relative/../traversal")]
    [InlineData(@"C:\absolute")]
    [InlineData(@"C:drive-relative")]
    [InlineData(@"\\server\share")]
    [InlineData("https://example.test/path")]
    [InlineData("relative\\backslash")]
    [InlineData("relative/\u0000control")]
    public void Parse_rejects_noncanonical_or_host_bound_values(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => LogicalPath.Parse(value));

        Assert.Contains("logical path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_preserves_unicode_code_points_without_host_dependent_normalization()
    {
        var composed = LogicalPath.Parse("café/report.txt");
        var decomposed = LogicalPath.Parse("café/report.txt");

        Assert.NotEqual(composed, decomposed);
        Assert.Equal("café/report.txt", composed.Value);
        Assert.Equal("café/report.txt", decomposed.Value);
    }
}

public sealed class FileToolsStorageRootTests
{
    [Theory]
    [InlineData("folder//child")]
    [InlineData("/absolute")]
    [InlineData("folder/../child")]
    public void Storage_root_rejects_empty_rooted_or_dot_segments(string value)
    {
        Assert.Throws<ArgumentException>(() => new FileToolsStorageRoot(value));
    }

    [Theory]
    [InlineData(@"folder\child", "folder/child")]
    [InlineData("mfs:/mutable", "mfs:/mutable")]
    public void Storage_root_canonicalizes_known_legacy_separators_without_rewriting_provider_syntax(
        string value,
        string expected)
    {
        Assert.Equal(expected, new FileToolsStorageRoot(value).Value);
    }
}

public sealed class PortablePathTemplateTests
{
    [Fact]
    public void Expand_resolves_home_and_explicit_environment_tokens()
    {
        var result = PortablePathTemplate.Expand(
            "~/${APP_DATA}/workspace",
            "/home/tester",
            name => name == "APP_DATA" ? ".local/share" : null,
            PortablePathTemplateCompatibility.Canonical);

        Assert.Equal("/home/tester/.local/share/workspace", result);
    }

    [Fact]
    public void Expand_resolves_nested_tokens_with_a_bounded_pass_count()
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ROOT"] = "${BASE}/data",
            ["BASE"] = "/srv"
        };

        var result = PortablePathTemplate.Expand(
            "${ROOT}/workspace",
            "/home/tester",
            name => variables.GetValueOrDefault(name),
            PortablePathTemplateCompatibility.Canonical);

        Assert.Equal("/srv/data/workspace", result);
    }

    [Fact]
    public void Expand_supports_field_scoped_legacy_windows_tokens()
    {
        var result = PortablePathTemplate.Expand(
            @"%LOCALAPPDATA%\CanDoItAll",
            @"C:\Users\tester",
            name => name == "LOCALAPPDATA" ? @"C:\Users\tester\AppData\Local" : null,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal(@"C:\Users\tester\AppData\Local/CanDoItAll", result);
    }

    [Fact]
    public void Expand_normalizes_legacy_home_separator_without_changing_unix_home_value()
    {
        var result = PortablePathTemplate.Expand(
            @"~\workspace",
            "/home/tester",
            _ => null,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal("/home/tester/workspace", result);
    }

    [Fact]
    public void Expand_normalizes_legacy_variable_separator_without_changing_unix_variable_value()
    {
        var result = PortablePathTemplate.Expand(
            @"%APP_DATA%\child",
            "/home/tester",
            name => name == "APP_DATA" ? "/var/lib/candoitall" : null,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal("/var/lib/candoitall/child", result);
    }

    [Fact]
    public void Expand_preserves_literal_backslash_in_unix_path_after_home_expansion()
    {
        var result = PortablePathTemplate.Expand(
            @"~/folder\literal-name",
            "/home/tester",
            _ => null,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal(@"/home/tester/folder\literal-name", result);
    }

    [Fact]
    public void Expand_preserves_literal_backslash_after_canonical_variable_token()
    {
        var result = PortablePathTemplate.Expand(
            @"${ROOT}\literal-name",
            "/home/tester",
            name => name == "ROOT" ? "/srv/candoitall" : null,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal(@"/srv/candoitall\literal-name", result);
    }

    [Fact]
    public void Expand_preserves_escaped_tokens()
    {
        var result = PortablePathTemplate.Expand(
            "$${APP_DATA}/%%LOCALAPPDATA%%",
            "/home/tester",
            _ => throw new InvalidOperationException("Escaped tokens must not be resolved."),
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);

        Assert.Equal("${APP_DATA}/%LOCALAPPDATA%", result);
    }

    [Fact]
    public void Expand_reports_unset_variable_without_silently_preserving_the_token()
    {
        var exception = Assert.Throws<PortablePathTemplateException>(() =>
            PortablePathTemplate.Expand(
                "${MISSING}/workspace",
                "/home/tester",
                _ => null,
                PortablePathTemplateCompatibility.Canonical));

        Assert.Equal(PortablePathTemplateFailure.UnsetVariable, exception.Failure);
        Assert.Equal("MISSING", exception.VariableName);
        Assert.DoesNotContain("environment", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_reports_recursive_variable_expansion()
    {
        var exception = Assert.Throws<PortablePathTemplateException>(() =>
            PortablePathTemplate.Expand(
                "${LOOP}",
                "/home/tester",
                _ => "${LOOP}",
                PortablePathTemplateCompatibility.Canonical));

        Assert.Equal(PortablePathTemplateFailure.ExpansionLimitExceeded, exception.Failure);
    }
}
