using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Records every browser-side failure of a CRM / HR journey: page errors, console errors, failed requests (static
// assets, API calls and circuit traffic alike) and HTTP error responses. Nothing is filtered by URL family or by
// failure text alone. The only allowance is the teardown a journey causes itself: while NavigateAsync replaces the
// document, the page that goes away posts its circuit's disconnect beacon and the browser aborts that beacon with the
// document. Exactly that request (POST to the circuit's disconnect endpoint, net::ERR_ABORTED, during the journey's
// own navigation, at most once per navigation) is kept apart as expected teardown and stays visible in the evidence.
// Every other circuit request, including negotiate and long-polling traffic, counts as a failure.
internal sealed class CrmHrBrowserOracle
{
    private const string CircuitDisconnectPath = "/_blazor/disconnect";
    private const string AbortedFailure = "net::ERR_ABORTED";

    // One document replacement ends one circuit, which posts one disconnect beacon. The first navigation of a page
    // replaces no circuit.
    private const int TeardownRequestsPerReplacement = 1;

    private readonly IPage page;
    private readonly object gate = new();
    private readonly List<string> pageErrors = [];
    private readonly List<string> consoleErrors = [];
    private readonly List<string> requestFailures = [];
    private readonly List<string> httpErrors = [];
    private readonly List<string> expectedTeardown = [];
    private bool navigating;
    private int navigations;

    private CrmHrBrowserOracle(IPage page)
    {
        this.page = page;
    }

    public IReadOnlyList<string> ExpectedTeardown
    {
        get
        {
            lock (gate)
            {
                return expectedTeardown.ToArray();
            }
        }
    }

    public static CrmHrBrowserOracle Attach(IPage page)
    {
        var oracle = new CrmHrBrowserOracle(page);
        page.PageError += (_, message) => oracle.Record(oracle.pageErrors, message);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                oracle.Record(oracle.consoleErrors, message.Text);
            }
        };
        page.RequestFailed += (_, request) => oracle.OnRequestFailed(request);
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                oracle.Record(oracle.httpErrors, $"{response.Status} {response.Request.Method} {response.Url}");
            }
        };
        return oracle;
    }

    // A full-page navigation issued by the journey itself. The teardown allowance is open only while it runs.
    public async Task<IResponse?> NavigateAsync(string url)
    {
        lock (gate)
        {
            navigating = true;
            navigations++;
        }

        try
        {
            return await page.GotoAsync(url);
        }
        finally
        {
            lock (gate)
            {
                navigating = false;
            }
        }
    }

    public async Task AssertCleanAsync()
    {
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync(), "The Blazor error UI is visible.");
        Assert.False(
            await page.Locator("#components-reconnect-modal.components-reconnect-show, #components-reconnect-modal.components-reconnect-failed, #components-reconnect-modal.components-reconnect-rejected").IsVisibleAsync(),
            "The circuit reconnect dialog is visible.");

        string[] errors, console, failures, http, teardown;
        int allowedTeardown;
        lock (gate)
        {
            errors = pageErrors.ToArray();
            console = consoleErrors.ToArray();
            failures = requestFailures.ToArray();
            http = httpErrors.ToArray();
            teardown = expectedTeardown.ToArray();
            allowedTeardown = Math.Max(0, navigations - 1) * TeardownRequestsPerReplacement;
        }

        Assert.True(errors.Length == 0, "Page errors: " + string.Join(" | ", errors));
        Assert.True(console.Length == 0, "Console errors: " + string.Join(" | ", console));
        Assert.True(failures.Length == 0, "Failed requests: " + string.Join(" | ", failures));
        Assert.True(http.Length == 0, "HTTP error responses: " + string.Join(" | ", http));
        Assert.True(
            teardown.Length <= allowedTeardown,
            $"More circuit teardown requests than {navigations} navigation(s) explain: " + string.Join(" | ", teardown));
    }

    private void OnRequestFailed(IRequest request)
    {
        var description = $"{request.Method} {request.Url}: {request.Failure}";
        lock (gate)
        {
            if (navigating &&
                string.Equals(request.Method, "POST", StringComparison.Ordinal) &&
                string.Equals(request.Failure, AbortedFailure, StringComparison.Ordinal) &&
                Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) &&
                string.Equals(uri.AbsolutePath, CircuitDisconnectPath, StringComparison.Ordinal))
            {
                expectedTeardown.Add(description);
                return;
            }

            requestFailures.Add(description);
        }
    }

    private void Record(List<string> target, string message)
    {
        lock (gate)
        {
            target.Add(message);
        }
    }
}
