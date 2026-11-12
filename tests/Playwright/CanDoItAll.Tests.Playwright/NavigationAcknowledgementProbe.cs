using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class NavigationAcknowledgementProbe {
    public static async Task WaitForCompletionAsync(IPage page, PlaywrightAppFixture host, int logStart) {
        await page.WaitForFunctionAsync("() => navigationAcknowledgementProbe.records.length > 0 && navigationAcknowledgementProbe.records.every(r => r.sentUtc && r.succeeded)");
        var uris = await page.EvaluateAsync<string[]>("() => navigationAcknowledgementProbe.records.map(r => r.uri)");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true) {
            var lines = host.GetLogLines().Skip(logStart).ToArray();
            foreach (var group in uris.GroupBy(uri => uri)) {
                if (lines.Count(line => line.Contains("Requesting navigation to URI " + group.Key + " with ", StringComparison.Ordinal)) > group.Count()) {
                    throw new InvalidOperationException("Another navigation shares the observed URI; completion cannot be attributed to this page.");
                }
            }
            if (uris.GroupBy(uri => uri).All(group => lines.Count(line => line.EndsWith(
                    "Navigation completed when changing the location to " + group.Key, StringComparison.Ordinal)) >= group.Count())) {
                return;
            }
            await Task.Delay(25, timeout.Token);
        }
    }

    public static Task InstallAsync(IBrowserContext context) => context.AddInitScriptAsync("""
        (() => {
            const records = [];
            const pending = new Map();
            const dispatchers = [];
            let hold = false;
            window.navigationAcknowledgementProbe = {
                records,
                retire: () => Promise.all(dispatchers.map(dispatcher => dispatcher.dispose())),
                hold: () => { hold = true; },
                release: () => {
                    hold = false;
                    for (const send of pending.values()) send();
                    pending.clear();
                }
            };
            let dotNet;
            Object.defineProperty(window, 'DotNet', {
                configurable: true,
                get: () => dotNet,
                set: value => {
                    dotNet = value;
                    const attach = value.attachDispatcher;
                    value.attachDispatcher = dispatcher => {
                        dispatchers.push(dispatcher);
                        const calls = attach(dispatcher);
                        const begin = calls.beginInvokeJSFromDotNet.bind(calls);
                        const end = dispatcher.endInvokeJSFromDotNet.bind(dispatcher);
                        const navigations = new Map();
                        calls.beginInvokeJSFromDotNet = (handle, identifier, args, ...rest) => {
                            if (identifier === 'Blazor._internal.navigationManager.navigateTo') {
                                if (records.length >= 128) throw new Error('Navigation probe bound exceeded');
                                const record = { handle, connectionId: dispatcher._connection.connectionId, uri: JSON.parse(args)[0], invokedUtc: new Date().toISOString(), returnedUtc: null, sentUtc: null, succeeded: null };
                                navigations.set(handle, record);
                                records.push(record);
                            }
                            return begin(handle, identifier, args, ...rest);
                        };
                        dispatcher.endInvokeJSFromDotNet = (handle, succeeded, args) => {
                            const record = navigations.get(handle);
                            if (!record) return end(handle, succeeded, args);
                            record.returnedUtc = new Date().toISOString();
                            record.succeeded = succeeded;
                            const send = () => {
                                end(handle, succeeded, args);
                                record.sentUtc = new Date().toISOString();
                                navigations.delete(handle);
                            };
                            if (hold) pending.set(handle, send);
                            else send();
                        };
                        return calls;
                    };
                }
            });
        })();
        """);

    public static Task HoldAsync(IPage page) => page.EvaluateAsync("() => navigationAcknowledgementProbe.hold()");
    public static Task ReleaseAsync(IPage page) => page.EvaluateAsync("() => navigationAcknowledgementProbe.release()");
}
