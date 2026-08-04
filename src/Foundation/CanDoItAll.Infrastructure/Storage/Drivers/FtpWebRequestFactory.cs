using System.Net;

namespace CanDoItAll.Infrastructure.Storage;

internal static class FtpWebRequestFactory
{
    public static FtpWebRequest Create(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        string method)
    {
        StorageProviderConfiguration configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        Uri requestUri = FtpStorageAddressPolicy.ResolveObjectUri(storage, remotePath);
#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(requestUri);
#pragma warning restore SYSLIB0014
        request.Method = method;
        request.UseBinary = true;
        request.UsePassive = configuration.UsePassiveMode;
        request.EnableSsl = configuration.UseSsl;
        request.KeepAlive = true;
        if (!string.IsNullOrWhiteSpace(configuration.Username))
        {
            request.Credentials = new NetworkCredential(configuration.Username, password ?? string.Empty);
        }

        return request;
    }

    public static async Task<FtpWebResponse> GetResponseAsync(
        FtpWebRequest request,
        CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration registration = cancellationToken.Register(request.Abort);
        return (FtpWebResponse)await request.GetResponseAsync().WaitAsync(cancellationToken);
    }

    public static async Task EnsureParentDirectoriesAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        CancellationToken cancellationToken)
    {
        string[] segments = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string currentPath = string.Empty;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            currentPath = CombineRemotePath(currentPath, segments[index]);
            try
            {
                FtpWebRequest request = Create(storage, password, currentPath, WebRequestMethods.Ftp.MakeDirectory);
                using FtpWebResponse response = await GetResponseAsync(request, cancellationToken);
            }
            catch (WebException exception) when (
                exception.Response is FtpWebResponse ftpResponse &&
                ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
            {
                ftpResponse.Dispose();
            }
        }
    }

    public static bool IsMachineListingUnsupported(WebException exception)
        => exception.Response is FtpWebResponse response &&
           response.StatusCode is
               FtpStatusCode.CommandSyntaxError or
               FtpStatusCode.ArgumentSyntaxError or
               FtpStatusCode.CommandNotImplemented or
               FtpStatusCode.BadCommandSequence;

    public static bool IsFileNotFound(WebException exception)
    {
        if (exception.Response is not FtpWebResponse
            {
                StatusCode: FtpStatusCode.ActionNotTakenFileUnavailable
            } response)
        {
            return false;
        }

        return IsFileNotFound(response.StatusCode, response.StatusDescription);
    }

    internal static bool IsFileNotFound(
        FtpStatusCode statusCode,
        string? statusDescription)
    {
        if (statusCode != FtpStatusCode.ActionNotTakenFileUnavailable)
        {
            return false;
        }

        var description = statusDescription ?? string.Empty;
        return description.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("no such file", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("cannot find", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("can't find", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineRemotePath(string parent, string name)
        => string.Join('/', new[] { parent.Trim('/'), name.Trim('/') }.Where(value => value.Length > 0));
}
