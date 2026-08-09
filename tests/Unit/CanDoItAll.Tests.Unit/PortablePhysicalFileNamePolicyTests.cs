using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class PortablePhysicalFileNamePolicyTests
{
    [Theory]
    [InlineData("a:b.txt", "a-b.txt~1f0f1e95b083")]
    [InlineData("CON", "_CON~a3dbc4b644a9")]
    [InlineData("trail.", "trail~cedd4b9ff43e")]
    [InlineData("é.txt", "é.txt~5c89df124ca0")]
    public void Encode_has_cross_host_golden_output(string displayName, string expectedPhysicalName)
    {
        PortablePhysicalFileName result = PortablePhysicalFileNamePolicy.Encode(displayName);

        Assert.Equal(expectedPhysicalName, result.PhysicalName);
        Assert.Equal(displayName, result.DisplayName);
    }

    [Theory]
    [InlineData("folder/name.txt")]
    [InlineData("folder\\name.txt")]
    [InlineData("question?.txt")]
    [InlineData("quote\".txt")]
    [InlineData("control\u0001.txt")]
    public void Encode_never_emits_nonportable_path_or_control_characters(string displayName)
    {
        PortablePhysicalFileName result = PortablePhysicalFileNamePolicy.Encode(displayName);

        Assert.DoesNotContain(result.PhysicalName, character =>
            character < ' ' || "<>:\"/\\|?*".Contains(character));
    }

    [Fact]
    public void Distinct_invalid_names_do_not_collapse_to_the_same_physical_name()
    {
        PortablePhysicalFileName colon = PortablePhysicalFileNamePolicy.Encode("report:final.txt");
        PortablePhysicalFileName question = PortablePhysicalFileNamePolicy.Encode("report?final.txt");

        Assert.NotEqual(colon.PhysicalName, question.PhysicalName);
    }

    [Fact]
    public void Safe_name_is_preserved_without_hash_suffix()
    {
        PortablePhysicalFileName result = PortablePhysicalFileNamePolicy.Encode("quarterly report.txt");

        Assert.Equal("quarterly report.txt", result.PhysicalName);
    }

    [Fact]
    public void Case_collision_on_insensitive_root_gets_deterministic_suffix()
    {
        PortablePhysicalFileName first = PortablePhysicalFileNamePolicy.Allocate(
            "readme.md",
            ["README.md"],
            StringComparer.OrdinalIgnoreCase);
        PortablePhysicalFileName second = PortablePhysicalFileNamePolicy.Allocate(
            "readme.md",
            ["README.md"],
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(first.PhysicalName, second.PhysicalName);
        Assert.StartsWith("readme.md~", first.PhysicalName, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_physical_name_cannot_alias_a_distinct_portable_display_name()
    {
        string generatedName = PortablePhysicalFileNamePolicy.Encode("report:final.txt").PhysicalName;

        PortablePhysicalFileName allocated = PortablePhysicalFileNamePolicy.Allocate(
            generatedName,
            [generatedName],
            StringComparer.Ordinal);

        Assert.NotEqual(generatedName, allocated.PhysicalName);
        Assert.StartsWith(generatedName + "~", allocated.PhysicalName, StringComparison.Ordinal);
    }

    [Fact]
    public void Allocation_rechecks_an_occupied_hash_suffix()
    {
        string generatedName = PortablePhysicalFileNamePolicy.Encode("report:final.txt").PhysicalName;
        string firstAlternative = PortablePhysicalFileNamePolicy.Allocate(
            generatedName,
            [generatedName],
            StringComparer.Ordinal).PhysicalName;

        PortablePhysicalFileName allocated = PortablePhysicalFileNamePolicy.Allocate(
            generatedName,
            [generatedName, firstAlternative],
            StringComparer.Ordinal);

        Assert.DoesNotContain(
            allocated.PhysicalName,
            new[] { generatedName, firstAlternative });
        Assert.EndsWith("-2", allocated.PhysicalName, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_unicode_name_is_truncated_on_rune_boundary_within_utf8_budget()
    {
        string displayName = string.Concat(Enumerable.Repeat("😀", 100)) + ".txt";

        PortablePhysicalFileName result = PortablePhysicalFileNamePolicy.Encode(displayName);

        Assert.InRange(
            Encoding.UTF8.GetByteCount(result.PhysicalName),
            1,
            PortablePhysicalFileNamePolicy.DefaultMaximumUtf8Bytes);
        Assert.DoesNotContain('\uFFFD', result.PhysicalName);
        Assert.Equal(displayName, result.DisplayName);
    }

    [Theory]
    [InlineData("COM1")]
    [InlineData("lpt9.txt")]
    [InlineData("NUL.json")]
    public void Reserved_device_names_are_never_emitted_verbatim(string displayName)
    {
        PortablePhysicalFileName result = PortablePhysicalFileNamePolicy.Encode(displayName);

        Assert.False(string.Equals(displayName, result.PhysicalName, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("report.txt", true)]
    [InlineData("report:final.txt", false)]
    [InlineData("CON", false)]
    [InlineData("trail.", false)]
    [InlineData("é.txt", false)]
    public void IsPortable_uses_the_same_cross_host_policy(string value, bool expected)
        => Assert.Equal(expected, PortablePhysicalFileNamePolicy.IsPortable(value));

    [Fact]
    public void FileSafeSlugBuilder_is_deterministic_for_portability_sensitive_input()
    {
        string first = FileSafeSlugBuilder.Build("Quarter: Final");
        string second = FileSafeSlugBuilder.Build("Quarter: Final");

        Assert.Equal(first, second);
        Assert.DoesNotContain(':', first);
    }
}
