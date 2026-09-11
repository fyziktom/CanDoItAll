using System.Runtime.ExceptionServices;

namespace CanDoItAll.Tests.Support;

public static class TestFixtureCleanup {
    public static async ValueTask DisposeAsync(params IAsyncDisposable?[] resources) {
        List<Exception>? failures = null;
        foreach (var resource in resources) {
            if (resource is null) {
                continue;
            }

            try {
                await resource.DisposeAsync();
            } catch (Exception failure) {
                (failures ??= []).Add(failure);
            }
        }

        if (failures is null) {
            return;
        }

        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("Disposing test fixture resources failed.", failures);
    }

    public static async ValueTask DisposeAfterFailureAsync(Exception failure, params IAsyncDisposable?[] resources) {
        try {
            await DisposeAsync(resources);
        } catch (Exception cleanupFailure) {
            throw new AggregateException("Test fixture setup failed and cleanup also failed.", failure, cleanupFailure);
        }
    }
}
