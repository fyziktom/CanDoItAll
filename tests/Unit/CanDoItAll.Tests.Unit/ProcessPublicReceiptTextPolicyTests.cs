using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessPublicReceiptTextPolicyTests
{
    [Theory]
    [InlineData(@"Open ""C:\Users\Jane Doe\secret.txt"" now.", "Jane Doe")]
    [InlineData("Open '/home/Jane Doe/secret.txt' now.", "Jane Doe")]
    [InlineData(@"root:C:\private\host\receipt.txt", @"C:\private")]
    [InlineData("root:/home/private/receipt.txt", "/home/private")]
    [InlineData(@"Read \\server\Private Share\receipt.txt", "Private Share")]
    [InlineData("Read file:///home/Jane Doe/receipt.txt", "Jane Doe")]
    [InlineData(@"Use `C:\private\host\receipt.txt` now.", @"C:\private")]
    [InlineData("Use `/home/private/receipt.txt` now.", "/home/private")]
    [InlineData(@"Use *C:\private\host\receipt.txt* now.", @"C:\private")]
    [InlineData("Use >/home/private/receipt.txt now.", "/home/private")]
    [InlineData("Use vscode://file/C:/Users/Jane/receipt.txt", "Users/Jane")]
    [InlineData(@"!C:\Users\private\receipt.txt", @"C:\Users\private")]
    [InlineData(@"$C:\private\receipt.txt", @"C:\private")]
    [InlineData("#/home/private/receipt.txt", "/home/private")]
    [InlineData(@"]C:\private\receipt.txt", @"C:\private")]
    [InlineData(@"xC:\Users\private\receipt.txt", @"C:\Users\private")]
    [InlineData("x/home/private/receipt.txt", "/home/private")]
    [InlineData(@"x\\server\private\receipt.txt", @"\\server\private")]
    [InlineData("/home/private,segment/token.txt then continue", "segment/token.txt")]
    [InlineData(@"C:\Users\Jane]Private\token.txt then continue", @"Private\token.txt")]
    public void Sanitize_removes_complete_cross_host_physical_path_expressions(
        string input,
        string sensitiveFragment)
    {
        var sanitized = ProcessPublicReceiptTextPolicy.Sanitize(input);

        Assert.Contains("[physical path removed]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveFragment, sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.invalid/docs/path")]
    [InlineData("http://127.0.0.1:5032/status")]
    public void Sanitize_preserves_non_file_uri_references(string input)
    {
        Assert.Equal(input, ProcessPublicReceiptTextPolicy.Sanitize(input));
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(input, 1024));
    }

    [Fact]
    public void Sanitize_removes_http_userinfo_without_discarding_the_remote_reference()
    {
        const string input = "https://alice:supersecret@example.invalid/api";

        var sanitized = ProcessPublicReceiptTextPolicy.Sanitize(input);

        Assert.Equal("https://[credentials removed]@example.invalid/api", sanitized);
        Assert.DoesNotContain("alice", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", sanitized, StringComparison.Ordinal);
        Assert.False(ProcessPublicReceiptTextPolicy.IsSafe(input, 1024));
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(sanitized, 1024));
    }

    [Theory]
    [InlineData("https://example.invalid/object?X-Amz-Signature=raw-signature", "raw-signature")]
    [InlineData("https://example.invalid/callback#code=oauth-code", "oauth-code")]
    public void Sanitize_removes_http_query_and_fragment_credentials(
        string input,
        string credential)
    {
        var sanitized = ProcessPublicReceiptTextPolicy.Sanitize(input);

        Assert.Equal("https://example.invalid/object[url parameters removed]", sanitized.Replace("callback", "object", StringComparison.Ordinal));
        Assert.DoesNotContain(credential, sanitized, StringComparison.Ordinal);
        Assert.False(ProcessPublicReceiptTextPolicy.IsSafe(input, 1024));
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(sanitized, 1024));
    }

    [Fact]
    public void NormalizePublicMessages_redacts_bounds_deduplicates_and_caps_public_output()
    {
        var values = Enumerable.Range(0, ProcessPublicReceiptTextPolicy.MaximumPublicMessageCount + 5)
            .Select(index => index == 0
                ? $"password=raw-secret at C:\\private\\host\\receipt-{index}.txt {new string('x', 3_000)}"
                : $"Diagnostic {index}")
            .Append("Diagnostic 1")
            .ToArray();

        var normalized = ProcessPublicReceiptTextPolicy.NormalizePublicMessages(values);

        Assert.Equal(ProcessPublicReceiptTextPolicy.MaximumPublicMessageCount, normalized.Count);
        Assert.All(normalized, value => Assert.InRange(
            value.Length,
            1,
            ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength));
        Assert.DoesNotContain(normalized, value => value.Contains("raw-secret", StringComparison.Ordinal));
        Assert.DoesNotContain(normalized, value => value.Contains(@"C:\private\host", StringComparison.Ordinal));
        Assert.Single(normalized, value => string.Equals(value, "Diagnostic 1", StringComparison.Ordinal));
    }
}
