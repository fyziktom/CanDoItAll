namespace CanDoItAll.Modules.Resources;

public static class ResourceConnectorPluginKeys
{
    public const string Repository = "resource.repository";
    public const string Folder = "resource.folder";
    public const string File = "resource.file";
    public const string WebLink = "resource.web-link";
    public const string Ftp = "resource.ftp";
    public const string Ssh = "resource.ssh";
    public const string PowerShellScript = "resource.powershell-script";
    public const string DockerCompose = "resource.docker-compose";
    public const string SecretLink = "resource.secret-link";
    public const string PromptLink = "resource.prompt-link";
    public const string WebhookEndpoint = "resource.webhook-endpoint";
    public const string StorageObject = "resource.storage-object";
}

public static class ResourceConnectorFieldKeys
{
    public const string RepositoryUrl = "repositoryUrl";
    public const string DefaultBranch = "defaultBranch";
    public const string RelativePath = "relativePath";
    public const string FolderPath = "folderPath";
    public const string FilePath = "filePath";
    public const string WorkingDirectory = "workingDirectory";
    public const string WebUrl = "webUrl";
    public const string UrlTitleHint = "urlTitleHint";
    public const string Host = "host";
    public const string Port = "port";
    public const string RemotePath = "remotePath";
    public const string UserName = "userName";
    public const string ScriptPath = "scriptPath";
    public const string ScriptArguments = "scriptArguments";
    public const string ComposeFilePath = "composeFilePath";
    public const string ComposeService = "composeService";
    public const string SecretPurpose = "secretPurpose";
    public const string PromptReference = "promptReference";
    public const string PromptTitleHint = "promptTitleHint";
    public const string EndpointUrl = "endpointUrl";
    public const string HealthPath = "healthPath";
    public const string HttpMethod = "method";
}
