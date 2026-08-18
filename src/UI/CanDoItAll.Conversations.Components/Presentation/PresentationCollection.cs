using System.Collections.Immutable;

namespace CanDoItAll.Conversations.Components.Presentation;

internal static class PresentationCollection
{
    public static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T>? source, string parameterName)
        where T : class
    {
        if (source is null || source.Count == 0)
        {
            return ImmutableArray<T>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<T>(source.Count);
        foreach (var item in source)
        {
            if (item is null)
            {
                throw new ArgumentException("Presentation collections cannot contain null entries.", parameterName);
            }

            builder.Add(item);
        }

        return builder.MoveToImmutable();
    }
}
