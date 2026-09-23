using System.Collections;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit.CrmHr;

// Every editor input of the CRM / HR contracts offers Snapshot(): the independent submission a host captures before
// its first await. The facts walk the public contract by reflection, so a property added to an editor input later is
// covered without editing this file, and a forgotten nested copy fails here.
public sealed class CrmHrEditorSnapshotTests
{
    public static TheoryData<Type> EditorInputs
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in SnapshotTypes())
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Fact]
    public void Every_editor_input_of_the_contracts_offers_a_snapshot()
    {
        var editorInputs = typeof(PartyEditorModel).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("EditorModel", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(editorInputs);
        Assert.All(editorInputs, type => Assert.Contains(type, SnapshotTypes()));
    }

    [Theory]
    [MemberData(nameof(EditorInputs))]
    public void Snapshot_is_a_complete_copy_with_independent_mutable_descendants(Type editorInput)
    {
        var seed = 1;
        var original = Populate(editorInput, ref seed);

        var snapshot = Snapshot(original);

        Assert.NotSame(original, snapshot);
        var expected = JsonSerializer.Serialize(original, editorInput);
        Assert.Equal(expected, JsonSerializer.Serialize(snapshot, editorInput));
        AssertIndependent(original, snapshot, editorInput.Name);

        // Later edits of the draft never reach the captured submission.
        Disturb(original);
        Assert.NotEqual(expected, JsonSerializer.Serialize(original, editorInput));
        Assert.Equal(expected, JsonSerializer.Serialize(snapshot, editorInput));
    }

    private static Type[] SnapshotTypes()
        => typeof(PartyEditorModel).Assembly.GetExportedTypes()
            .Concat(typeof(ProjectPartyAssignmentUpsertRequest).Assembly.GetExportedTypes())
            .Where(type => type.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is { } method && method.ReturnType == type)
            .ToArray();

    private static object Snapshot(object value)
        => value.GetType().GetMethod("Snapshot", Type.EmptyTypes)!.Invoke(value, null)!;

    private static bool HasSnapshot(Type type)
        => type.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null;

    private static object Populate(Type type, ref int seed)
    {
        var instance = Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            property.SetValue(instance, Value(property.PropertyType, ref seed));
        }

        return instance;
    }

    private static object? Value(Type type, ref int seed)
    {
        seed++;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return $"value-{seed}";
        if (underlying == typeof(Guid)) return new Guid(seed, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        if (underlying == typeof(bool)) return true;
        if (underlying == typeof(int)) return seed;
        if (underlying == typeof(long)) return (long)seed;
        if (underlying == typeof(decimal)) return seed + 0.25m;
        if (underlying == typeof(double)) return seed + 0.5d;
        if (underlying == typeof(DateOnly)) return new DateOnly(2026, 1, 1).AddDays(seed);
        if (underlying == typeof(DateTime)) return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(seed);
        if (underlying == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(seed);
        if (underlying.IsEnum) return Enum.GetValues(underlying).Cast<object>().Last();
        if (underlying == typeof(ProjectWriteAdmission)) return new ProjectWriteAdmission(Id(ref seed), Id(ref seed), Id(ref seed));
        if (underlying == typeof(ProjectAssignmentReference)) return new ProjectAssignmentReference(Id(ref seed), Id(ref seed), Id(ref seed));
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(underlying)!;
            var element = underlying.GetGenericArguments()[0];
            list.Add(Value(element, ref seed));
            list.Add(Value(element, ref seed));
            return list;
        }

        if (underlying.IsClass && underlying.GetConstructor(Type.EmptyTypes) is not null)
        {
            return Populate(underlying, ref seed);
        }

        throw new InvalidOperationException($"The snapshot fact cannot populate {type.FullName}; teach it the type so the copy stays proven.");
    }

    private static Guid Id(ref int seed) => new(++seed, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9);

    private static void AssertIndependent(object original, object snapshot, string path)
    {
        foreach (var property in original.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            var left = property.GetValue(original);
            var right = property.GetValue(snapshot);
            if (left is IList leftList && right is IList rightList)
            {
                Assert.False(ReferenceEquals(leftList, rightList), $"{path}.{property.Name} shares its list with the snapshot");
                for (var index = 0; index < leftList.Count; index++)
                {
                    if (leftList[index] is { } leftItem && rightList[index] is { } rightItem && HasSnapshot(leftItem.GetType()))
                    {
                        Assert.False(ReferenceEquals(leftItem, rightItem), $"{path}.{property.Name}[{index}] shares a nested input with the snapshot");
                        AssertIndependent(leftItem, rightItem, $"{path}.{property.Name}[{index}]");
                    }
                }
            }
        }
    }

    private static void Disturb(object original)
    {
        foreach (var property in original.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            switch (property.GetValue(original))
            {
                case string text:
                    property.SetValue(original, text + "-edited");
                    break;
                case Guid id:
                    property.SetValue(original, new Guid(id.ToByteArray().Reverse().ToArray()));
                    break;
                case IList list when list.Count > 0:
                    if (list[0] is { } first && HasSnapshot(first.GetType()))
                    {
                        Disturb(first);
                    }

                    list.RemoveAt(list.Count - 1);
                    break;
            }
        }
    }
}
