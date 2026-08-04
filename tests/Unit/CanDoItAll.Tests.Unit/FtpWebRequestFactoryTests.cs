using System.Net;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class FtpWebRequestFactoryTests
{
    [Theory]
    [InlineData("550 File not found")]
    [InlineData("550 No such file")]
    [InlineData("550 Object does not exist")]
    [InlineData("550 Cannot find the requested path")]
    [InlineData("550 Can't find object")]
    public void Missing_classifier_accepts_only_proven_missing_descriptions(
        string description)
    {
        Assert.True(FtpWebRequestFactory.IsFileNotFound(
            FtpStatusCode.ActionNotTakenFileUnavailable,
            description));
    }

    [Theory]
    [InlineData(FtpStatusCode.ActionNotTakenFileUnavailable, "550 Permission denied")]
    [InlineData(FtpStatusCode.ActionNotTakenFileUnavailable, "550 Access denied")]
    [InlineData(FtpStatusCode.ActionNotTakenFileUnavailable, "550 Operation unavailable")]
    [InlineData(FtpStatusCode.NotLoggedIn, "530 File not found")]
    public void Missing_classifier_rejects_ambiguous_or_non_550_failures(
        FtpStatusCode statusCode,
        string description)
    {
        Assert.False(FtpWebRequestFactory.IsFileNotFound(
            statusCode,
            description));
    }
}
