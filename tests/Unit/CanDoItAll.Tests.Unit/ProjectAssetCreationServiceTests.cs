using System.Text;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectAssetCreationServiceTests
{
    [Theory]
    [InlineData(ProjectFileSubtype.Text, "notes", "notes.txt", "text/plain")]
    [InlineData(ProjectFileSubtype.Json, "settings.JSON", "settings.json", "application/json")]
    [InlineData(ProjectFileSubtype.Markdown, "README.markdown", "README.md", "text/markdown")]
    [InlineData(ProjectFileSubtype.Mermaid, "flow.mermaid", "flow.mmd", "text/vnd.mermaid")]
    public async Task Create_text_uses_canonical_file_name_content_type_and_utf8(
        ProjectFileSubtype subtype,
        string requestedFileName,
        string expectedFileName,
        string expectedContentType)
    {
        const string content = "{\"label\":\"Zażółć gęślą jaźń\"}";
        ProjectAssetCreationService service = BuildService();

        ProjectObjectMediaPayload media = await service.CreateTextAsync(subtype, requestedFileName, content);

        Assert.Equal(expectedFileName, media.FileName);
        Assert.Equal(expectedContentType, media.ContentType);
        Assert.Equal(content, Encoding.UTF8.GetString(Convert.FromBase64String(media.Base64Data)));
    }

    [Fact]
    public async Task Create_text_rejects_invalid_json_without_rewriting_the_content()
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => service.CreateTextAsync(ProjectFileSubtype.Json, "settings.json", "{ invalid }").AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidJson, exception.Code);
    }

    [Theory]
    [InlineData(ProjectFileSubtype.Json, "settings.txt")]
    [InlineData(ProjectFileSubtype.Markdown, "../README.md")]
    [InlineData(ProjectFileSubtype.Text, "folder\\notes.txt")]
    [InlineData(ProjectFileSubtype.Mermaid, ".mmd")]
    public async Task Create_text_rejects_unsafe_or_mismatched_file_names(
        ProjectFileSubtype subtype,
        string fileName)
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => service.CreateTextAsync(subtype, fileName, "content").AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidFileName, exception.Code);
    }

    [Fact]
    public async Task Create_text_rejects_non_text_file_subtypes()
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => service.CreateTextAsync(ProjectFileSubtype.Excel, "budget", "content").AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.UnsupportedFileSubtype, exception.Code);
    }

    [Fact]
    public async Task Create_text_rejects_empty_content_before_legacy_media_adaptation()
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => service.CreateTextAsync(ProjectFileSubtype.Markdown, "README", string.Empty).AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidContent, exception.Code);
    }

    [Fact]
    public async Task Create_text_rejects_content_larger_than_the_editable_file_limit()
    {
        ProjectAssetCreationService service = BuildService();
        string content = new('a', ProjectAssetCreationLimits.MaximumEditableTextBytes + 1);

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => service.CreateTextAsync(ProjectFileSubtype.Text, "large", content).AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.ContentTooLarge, exception.Code);
    }

    [Fact]
    public void Adapt_upload_preserves_valid_evidence_and_encodes_once()
    {
        ProjectAssetCreationService service = BuildService();
        byte[] content = [0, 1, 2, 127, 255];

        ProjectObjectMediaPayload media = service.AdaptUpload(
            "evidence.bin",
            "application/octet-stream",
            content);

        Assert.Equal("evidence.bin", media.FileName);
        Assert.Equal("application/octet-stream", media.ContentType);
        Assert.Equal(content, Convert.FromBase64String(media.Base64Data));
    }

    [Theory]
    [InlineData(ProjectFileSubtype.Text, "notes.txt", "application/octet-stream", "notes.txt", "text/plain")]
    [InlineData(ProjectFileSubtype.Json, "settings.json", "", "settings.json", "application/json")]
    [InlineData(ProjectFileSubtype.Markdown, "README.markdown", "text/plain", "README.md", "text/markdown")]
    [InlineData(ProjectFileSubtype.Mermaid, "flow.mmd", "", "flow.mmd", "text/vnd.mermaid")]
    public void Adapt_text_upload_validates_extension_and_canonicalizes_untrusted_media_type(
        ProjectFileSubtype subtype,
        string fileName,
        string advisoryContentType,
        string expectedFileName,
        string expectedContentType)
    {
        ProjectAssetCreationService service = BuildService();
        byte[] content = Encoding.UTF8.GetBytes(
            subtype == ProjectFileSubtype.Json ? "{\"enabled\":true}" : "content");

        ProjectObjectMediaPayload media = service.AdaptTextUpload(
            subtype,
            fileName,
            advisoryContentType,
            content);

        Assert.Equal(expectedFileName, media.FileName);
        Assert.Equal(expectedContentType, media.ContentType);
        Assert.Equal(content, Convert.FromBase64String(media.Base64Data));
    }

    [Theory]
    [InlineData(ProjectFileSubtype.Text, "notes.md", "text/plain", ProjectAssetCreationErrorCode.InvalidFileName)]
    [InlineData(ProjectFileSubtype.Mermaid, "flow.mmd", "image/png", ProjectAssetCreationErrorCode.InvalidContentType)]
    [InlineData(ProjectFileSubtype.Excel, "budget.xlsx", "application/octet-stream", ProjectAssetCreationErrorCode.UnsupportedFileSubtype)]
    public void Adapt_text_upload_rejects_conflicting_format_evidence(
        ProjectFileSubtype subtype,
        string fileName,
        string contentType,
        ProjectAssetCreationErrorCode expectedCode)
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => service.AdaptTextUpload(subtype, fileName, contentType, new byte[] { 1 }));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void Adapt_text_upload_rejects_invalid_utf8()
    {
        ProjectAssetCreationService service = BuildService();
        byte[] invalidUtf8 = [0xC3, 0x28];

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => service.AdaptTextUpload(
                ProjectFileSubtype.Text,
                "notes.txt",
                "text/plain",
                invalidUtf8));

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidContent, exception.Code);
    }

    [Fact]
    public void Adapt_text_upload_rejects_invalid_json_syntax()
    {
        ProjectAssetCreationService service = BuildService();
        byte[] invalidJson = Encoding.UTF8.GetBytes("{ invalid }");

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => service.AdaptTextUpload(
                ProjectFileSubtype.Json,
                "settings.json",
                "application/json",
                invalidJson));

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidJson, exception.Code);
    }

    [Fact]
    public void Adapt_encoded_text_upload_applies_the_same_validation_and_canonicalization()
    {
        ProjectAssetCreationService service = BuildService();
        byte[] content = Encoding.UTF8.GetBytes("{\"enabled\":true}");

        ProjectObjectMediaPayload media = service.AdaptEncodedTextUpload(
            ProjectFileSubtype.Json,
            "SETTINGS.JSON",
            "application/octet-stream",
            Convert.ToBase64String(content));

        Assert.Equal("SETTINGS.json", media.FileName);
        Assert.Equal("application/json", media.ContentType);
        Assert.Equal(content, Convert.FromBase64String(media.Base64Data));
    }

    [Fact]
    public void Adapt_encoded_text_upload_rejects_invalid_transport_content()
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => service.AdaptEncodedTextUpload(
                ProjectFileSubtype.Text,
                "notes.txt",
                "text/plain",
                "not-base64"));

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidContent, exception.Code);
    }

    [Theory]
    [InlineData("folder/evidence.txt", "text/plain", ProjectAssetCreationErrorCode.InvalidFileName)]
    [InlineData("evidence.txt", "", ProjectAssetCreationErrorCode.InvalidContentType)]
    public void Adapt_upload_rejects_invalid_transport_metadata(
        string fileName,
        string contentType,
        ProjectAssetCreationErrorCode expectedCode)
    {
        ProjectAssetCreationService service = BuildService();

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => service.AdaptUpload(fileName, contentType, new byte[] { 1 }));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void Resolver_rejects_duplicate_strategy_registrations()
    {
        var first = new ProjectTextAssetContentGenerator();
        var second = new ProjectTextAssetContentGenerator();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ProjectAssetContentGeneratorResolver([first, second]));

        Assert.Contains(nameof(ProjectAssetGenerationKind.Text), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolver_rejects_unsupported_generation_kind_without_a_fallback()
    {
        var resolver = new ProjectAssetContentGeneratorResolver([new ProjectTextAssetContentGenerator()]);

        ProjectAssetCreationException exception = Assert.Throws<ProjectAssetCreationException>(
            () => resolver.Resolve(ProjectAssetGenerationKind.Image));

        Assert.Equal(ProjectAssetCreationErrorCode.UnsupportedGenerationKind, exception.Code);
    }

    [Fact]
    public async Task Text_generator_rejects_a_request_of_the_wrong_contract_type()
    {
        var generator = new ProjectTextAssetContentGenerator();

        ProjectAssetCreationException exception = await Assert.ThrowsAsync<ProjectAssetCreationException>(
            () => generator.GenerateAsync(new WrongTextRequest()).AsTask());

        Assert.Equal(ProjectAssetCreationErrorCode.InvalidGeneratorRequest, exception.Code);
    }

    private static ProjectAssetCreationService BuildService()
        => new(new ProjectAssetContentGeneratorResolver([new ProjectTextAssetContentGenerator()]));

    private sealed record WrongTextRequest()
        : ProjectAssetContentGenerationRequest(ProjectAssetGenerationKind.Text);
}
