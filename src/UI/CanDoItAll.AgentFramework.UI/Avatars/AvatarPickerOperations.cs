using CanDoItAll.AgentFramework.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.UI.Avatars;

public sealed record AvatarPickerOperations(
    Func<CancellationToken, Task<AvatarGenerationSource?>> ReadSource,
    Func<AvatarGenerationRequest, CancellationToken, Task<AvatarGenerationResult>> Generate,
    Func<IBrowserFile, CancellationToken, Task<string>> Upload);

public enum AvatarPickerNoticeKind { Unavailable, Uploaded, UploadFailed, Generated, GenerationFailed }

public sealed record AvatarPickerNotice(AvatarPickerNoticeKind Kind, string Detail);
