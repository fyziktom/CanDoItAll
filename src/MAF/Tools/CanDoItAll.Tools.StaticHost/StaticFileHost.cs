using System.Net;
using Microsoft.AspNetCore.StaticFiles;

namespace CanDoItAll.Tools.StaticHost;

public static class StaticFileHost {
    public static async Task Main(string[] args) {
        if (args.Length != 3 || !bool.TryParse(args[2], out var spaFallback)) {
            throw new ArgumentException("Expected directory, loopback HTTP URL and SPA fallback flag.");
        }
        await using var app = Create(args[0], args[1], spaFallback);
        await app.RunAsync();
    }

    public static WebApplication Create(string directory, string url, bool spaFallback) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp ||
                !IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address) ||
                uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0) {
            throw new ArgumentException("Static validation hosting requires an HTTP loopback IP endpoint.");
        }
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root) || !IsSafePath(root, root)) {
            throw new ArgumentException("Static validation hosting requires an existing directory without symbolic links.");
        }
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Production"
        });
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(options => options.Listen(address, uri.Port));
        var app = builder.Build();
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".dll"] = "application/octet-stream";
        contentTypes.Mappings[".webcil"] = "application/octet-stream";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        app.Run(async context => {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            var relative = context.Request.Path.Value?.TrimStart('/') ?? string.Empty;
            if (relative.Split('/').Any(segment => segment is "." or ".." || segment.StartsWith('.')) ||
                    relative.Contains('\\') || relative.Contains(':') || relative.Contains('\0')) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var path = Path.GetFullPath(Path.Combine(root, relative.Length == 0 ? "index.html" : relative));
            if (!IsSafePath(root, path)) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (!File.Exists(path) && spaFallback && !Path.HasExtension(relative)) {
                path = Path.Combine(root, "index.html");
            }
            if (!IsSafePath(root, path) || !File.Exists(path) || !contentTypes.TryGetContentType(path, out var contentType)) {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = contentType;
            context.Response.Headers.CacheControl = "no-cache, max-age=0, must-revalidate";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.ContentLength = new FileInfo(path).Length;
            if (HttpMethods.IsGet(context.Request.Method)) {
                await context.Response.SendFileAsync(path, context.RequestAborted);
            }
        });
        return app;
    }

    private static bool IsSafePath(string root, string path) {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            return false;
        }
        for (var current = path; current is not null; current = Path.GetDirectoryName(current)) {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                return false;
            }
        }
        return true;
    }
}
