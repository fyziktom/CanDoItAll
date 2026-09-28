using System.Net;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed class ApiLoginThrottle(IClock clock) {
    private const int BucketCount = 2048;
    private readonly object coordination = new();
    private readonly long[] clientWindows = new long[BucketCount];
    private readonly int[] clientAttempts = new int[BucketCount];
    private readonly long[] accountWindows = new long[BucketCount];
    private readonly int[] accountAttempts = new int[BucketCount];

    public bool TryAcquire(HttpContext context, string? userName) {
        var normalizedName = ApiIdentityRules.NormalizeUserName(userName);
        var client = context.Connection.RemoteIpAddress ?? IPAddress.None;
        var clientBucket = (int)((uint)client.GetHashCode() % BucketCount);
        var accountBucket = (int)((uint)HashCode.Combine(client, normalizedName) % BucketCount);
        var window = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (coordination) {
            return Admit(clientWindows, clientAttempts, clientBucket, window, 30) &&
                Admit(accountWindows, accountAttempts, accountBucket, window, 10);
        }
    }

    private static bool Admit(long[] windows, int[] attempts, int bucket, long window, int limit) {
        if (windows[bucket] != window) {
            windows[bucket] = window;
            attempts[bucket] = 0;
        }
        if (attempts[bucket] >= limit) {
            return false;
        }
        attempts[bucket]++;
        return true;
    }
}
