using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed partial class SourceIngestionWorkflowExecutor
{
    private sealed record WorkflowSourceCandidate(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin);

    private sealed record WorkflowSourceIngestionFile(
        string FullPath,
        string DisplayPath,
        string FileName);

    private sealed record WorkflowSourceReadResult(
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionDocument(
        string Key,
        string Label,
        string Kind,
        string Origin,
        string Path,
        string FileName,
        string Extension,
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionError(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin,
        string Message);
}
