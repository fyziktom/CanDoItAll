using System.Collections;
using System.Collections.Immutable;
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
    private static readonly HashSet<Type> ObservationTypes = [
        typeof(AiStewardProfileObservation), typeof(AiAgentProfileObservation), typeof(AiProfileTechnicalInput),
        typeof(WorkforceProfileObservation), typeof(WorkforceProfileValues), typeof(PartySkillObservation), typeof(CapacityBlockObservation),
        typeof(SkillCatalogItemModel), typeof(StaffingRequestObservation), typeof(RecruitmentApplicationObservation),
        typeof(RecruitmentInterviewObservation), typeof(RecruitmentConversionObservation), typeof(LifecycleTaskObservation),
        typeof(CrmOpportunityConversionProjectBasis), typeof(CrmOpportunityAssignmentOriginal),
        typeof(ProjectAssignmentObservation), typeof(ProjectAssignmentPrimaryObservation),
        typeof(PartyRelationshipObservation), typeof(PartyRelationshipVersion), typeof(CrmAccountConnectionsObservation),
        typeof(CrmAccountConnectionOriginal), typeof(CrmAccountConnectionProjectOriginal)
    ];

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
        object instance;
        if (ObservationTypes.Contains(type)) {
            var constructor = type.GetConstructors().Single();
            var parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++) {
                arguments[index] = Value(parameters[index].ParameterType, ref seed);
            }
            instance = constructor.Invoke(arguments);
        } else {
            instance = Activator.CreateInstance(type)!;
        }
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
        if (underlying == typeof(uint)) {
            return (uint)seed;
        }
        if (underlying == typeof(decimal)) return seed + 0.25m;
        if (underlying == typeof(double)) return seed + 0.5d;
        if (underlying == typeof(DateOnly)) return new DateOnly(2026, 1, 1).AddDays(seed);
        if (underlying == typeof(DateTime)) return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(seed);
        if (underlying == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(seed);
        if (underlying.IsEnum) return Enum.GetValues(underlying).Cast<object>().Last();
        if (underlying == typeof(ProjectWriteAdmission)) return new ProjectWriteAdmission(Id(ref seed), Id(ref seed), Id(ref seed));
        if (underlying == typeof(ProjectAssignmentReference)) return new ProjectAssignmentReference(Id(ref seed), Id(ref seed), Id(ref seed));
        if (underlying == typeof(ImmutableArray<Guid>)) {
            return ImmutableArray.Create(Id(ref seed), Id(ref seed));
        }
        if (underlying == typeof(CrmAccountProfileObservation)) {
            return new CrmAccountProfileObservation(Id(ref seed), DateTimeOffset.UnixEpoch.AddMinutes(seed), DateTimeOffset.UnixEpoch.AddHours(seed));
        }
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(Dictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))) {
            var arguments = underlying.GetGenericArguments();
            var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments))!;
            dictionary.Add(Value(arguments[0], ref seed)!, Value(arguments[1], ref seed));
            dictionary.Add(Value(arguments[0], ref seed)!, Value(arguments[1], ref seed));
            return dictionary;
        }
        if (underlying.IsGenericType &&
            (underlying.GetGenericTypeDefinition() == typeof(List<>) || underlying.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            var element = underlying.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            list.Add(Value(element, ref seed));
            list.Add(Value(element, ref seed));
            return list;
        }

        if (ObservationTypes.Contains(underlying) || underlying.IsClass && underlying.GetConstructor(Type.EmptyTypes) is not null)
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
            if (left is IDictionary leftDictionary && right is IDictionary rightDictionary) {
                Assert.False(ReferenceEquals(leftDictionary, rightDictionary), $"{path}.{property.Name} shares its dictionary with the snapshot");
            }
            if (left is not null && right is not null && ObservationTypes.Contains(left.GetType())) {
                AssertIndependent(left, right, $"{path}.{property.Name}");
            }
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
                case CrmAccountProfileObservation observation:
                    property.SetValue(original, observation with { PartyUpdatedAtUtc = observation.PartyUpdatedAtUtc.AddSeconds(1) });
                    break;
                case string text:
                    property.SetValue(original, text + "-edited");
                    break;
                case Guid id:
                    property.SetValue(original, new Guid(id.ToByteArray().Reverse().ToArray()));
                    break;
                case ImmutableArray<Guid>:
                    break;
                case IDictionary dictionary when dictionary.Count > 0:
                    dictionary.Remove(dictionary.Keys.Cast<object>().First());
                    break;
                case IList list when list.Count > 0:
                    if (list[0] is { } first && HasSnapshot(first.GetType()))
                    {
                        Disturb(first);
                    }

                    list.RemoveAt(list.Count - 1);
                    break;
                case { } observation when ObservationTypes.Contains(observation.GetType()):
                    DisturbObservationCollections(observation);
                    var seed = 200;
                    property.SetValue(original, Value(property.PropertyType, ref seed));
                    break;
            }
        }
    }

    private static void DisturbObservationCollections(object observation) {
        foreach (var property in observation.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0)) {
            switch (property.GetValue(observation)) {
                case ImmutableArray<Guid>:
                    break;
                case IDictionary dictionary when dictionary.Count > 0:
                    dictionary.Remove(dictionary.Keys.Cast<object>().First());
                    break;
                case IList list when list.Count > 0:
                    list.RemoveAt(list.Count - 1);
                    break;
                case { } nested when ObservationTypes.Contains(nested.GetType()):
                    DisturbObservationCollections(nested);
                    break;
            }
        }
    }
}
