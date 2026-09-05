using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentEditorSnapshotContractTests {
    [Fact]
    public void Copy_preserves_every_public_property_and_owns_all_mutable_descendants() {
        var source = (AgentEditorModel)Sentinel(typeof(AgentEditorModel), null);
        var clone = AgentEditorDraftPolicy.Copy(source);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(clone));
        AssertIndependent(source, clone);
    }

    [Fact]
    public void Sections_are_complete_unique_and_round_trip_without_enum_ordinals() {
        Assert.Equal(Enum.GetValues<AgentEditorSection>().Order(), AgentEditorSections.All.Select(item => item.Section).Order());
        Assert.Equal(AgentEditorSections.All.Count, AgentEditorSections.All.Select(item => item.Section).Distinct().Count());
        foreach (var definition in AgentEditorSections.All) {
            Assert.Equal(definition, AgentEditorSections.At(AgentEditorSections.IndexOf(definition.Section)));
            Assert.False(string.IsNullOrWhiteSpace(definition.Label));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentEditorSections.At(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentEditorSections.At(AgentEditorSections.All.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentEditorSections.IndexOf((AgentEditorSection)(-1)));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("  explicit-model  ", "explicit-model")]
    public void Model_normalization_retains_explicit_values(string? value, string expected)
        => Assert.Equal(expected, ProviderModelValuePolicy.Normalize(value));

    private static object Sentinel(Type type, object? current) {
        if (Nullable.GetUnderlyingType(type) is { } nullableType) {
            return Sentinel(nullableType, current);
        }
        if (type == typeof(string)) {
            return "snapshot-sentinel";
        }
        if (type == typeof(bool)) {
            return current is not true;
        }
        if (type == typeof(Guid)) {
            return Guid.Parse("42000000-0000-0000-0000-000000000042");
        }
        if (type == typeof(DateTimeOffset)) {
            return DateTimeOffset.UnixEpoch.AddDays(42);
        }
        if (type == typeof(double)) {
            return 0.731d;
        }
        if (type == typeof(int)) {
            return 42;
        }
        if (type.IsEnum) {
            return Enum.GetValues(type).Cast<object>().First(value => !Equals(value, current ?? Activator.CreateInstance(type)));
        }
        if (type.IsArray) {
            var elementType = type.GetElementType()!;
            var array = Array.CreateInstance(elementType, 1);
            array.SetValue(Sentinel(elementType, null), 0);
            return array;
        }
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)) {
            var elementType = Assert.Single(type.GetGenericArguments());
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            list.Add(Sentinel(elementType, null));
            Assert.True(type.IsAssignableFrom(list.GetType()), $"Add a sentinel factory for {type}.");
            return list;
        }
        var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (parse is not null) {
            return parse.Invoke(null, ["snapshot-sentinel"])!;
        }
        object instance;
        if (type.GetConstructor(Type.EmptyTypes) is { } emptyConstructor) {
            instance = emptyConstructor.Invoke([]);
        } else {
            var constructor = type.GetConstructors().SingleOrDefault(candidate =>
                !candidate.GetParameters().Any(parameter => parameter.ParameterType == type));
            Assert.True(constructor is not null, $"Add a sentinel factory for {type}.");
            instance = constructor!.Invoke(constructor.GetParameters()
                .Select(parameter => Sentinel(parameter.ParameterType, null)).ToArray());
        }
        foreach (var property in Properties(type)) {
            property.SetValue(instance, Sentinel(property.PropertyType, property.GetValue(instance)));
        }
        return instance;
    }

    private static IEnumerable<PropertyInfo> Properties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null && property.SetMethod?.IsPublic == true);

    private static void AssertIndependent(object? source, object? clone) {
        if (source is null) {
            Assert.Null(clone);
            return;
        }
        Assert.NotNull(clone);
        var type = source.GetType();
        if (type == typeof(string) || type.IsValueType) {
            Assert.Equal(source, clone);
            return;
        }
        if (source is IEnumerable sequence) {
            Assert.NotSame(source, clone);
            var copiedItems = Assert.IsAssignableFrom<IEnumerable>(clone).Cast<object?>().ToArray();
            var items = sequence.Cast<object?>().ToArray();
            Assert.Equal(items.Length, copiedItems.Length);
            for (var index = 0; index < items.Length; index++) {
                AssertIndependent(items[index], copiedItems[index]);
            }
            return;
        }
        var properties = Properties(type).ToArray();
        if (properties.Any(property => !property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))) {
            Assert.NotSame(source, clone);
        }
        foreach (var property in properties) {
            AssertIndependent(property.GetValue(source), property.GetValue(clone));
        }
    }
}
