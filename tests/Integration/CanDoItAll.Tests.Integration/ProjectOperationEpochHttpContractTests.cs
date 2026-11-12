using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectOperationEpochHttpContractTests {
    private static readonly JsonSerializerOptions HttpJson = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Theory]
    [InlineData(1L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public void HTTP_epoch_is_lossless_while_default_persistence_serialization_remains_numeric(long value) {
        var epoch = new ProjectOperationEpoch(value);
        var canonical = value.ToString(CultureInfo.InvariantCulture);

        var wire = JsonSerializer.Serialize(epoch, HttpJson);

        Assert.Equal(JsonSerializer.Serialize(canonical), wire);
        Assert.Equal(epoch, JsonSerializer.Deserialize<ProjectOperationEpoch>(wire, HttpJson));
        Assert.True(ProjectOperationEpochHttpContract.TryParse(canonical, out var routeValue));
        Assert.Equal(epoch, routeValue);
        Assert.True(MatchesSchema(canonical));
        Assert.Equal(canonical, JsonSerializer.Serialize(epoch));
        Assert.Equal(epoch, JsonSerializer.Deserialize<ProjectOperationEpoch>(canonical));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("9007199254740993")]
    [InlineData("9223372036854775807")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"\"")]
    [InlineData("\"0\"")]
    [InlineData("\"01\"")]
    [InlineData("\"+1\"")]
    [InlineData("\"-1\"")]
    [InlineData("\" 1\"")]
    [InlineData("\"1 \"")]
    [InlineData("\"1\\n\"")]
    [InlineData("\"1.0\"")]
    [InlineData("\"1e1\"")]
    [InlineData("\"9223372036854775808\"")]
    [InlineData("\"10000000000000000000\"")]
    [InlineData("\"١\"")]
    public void HTTP_epoch_rejects_numbers_and_noncanonical_strings_without_coercion(string wire) {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectOperationEpoch>(wire, HttpJson));
        using var document = JsonDocument.Parse(wire);
        if (document.RootElement.ValueKind == JsonValueKind.String) {
            var value = document.RootElement.GetString()!;
            Assert.False(ProjectOperationEpochHttpContract.TryParse(value, out _));
            Assert.False(MatchesSchema(value));
        }
    }

    private static bool MatchesSchema(string value)
        => Regex.IsMatch(value, ProjectOperationEpochHttpContract.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}
