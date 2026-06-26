using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Api.Models;

public static class AnalysisResultDisplayFormatter
{
    public static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static string FormatDocumentReference(DocumentReference document)
    {
        var label = !string.IsNullOrWhiteSpace(document.FileName)
            ? GetDisplayDocumentName(document.FileName)
            : ValueOrDash(document.DocumentId);

        var context = new List<string>();

        if (!string.IsNullOrWhiteSpace(document.PageNumber))
        {
            context.Add($"page {document.PageNumber}");
        }

        if (!string.IsNullOrWhiteSpace(document.Section))
        {
            context.Add(document.Section);
        }

        if (document.LineNumber.HasValue)
        {
            context.Add($"line {document.LineNumber.Value}");
        }

        if (!string.IsNullOrWhiteSpace(document.ChunkId))
        {
            context.Add($"chunk {document.ChunkId}");
        }

        return context.Count == 0
            ? label
            : $"{label} ({string.Join(", ", context)})";
    }

    public static string GetDisplayDocumentName(string sourceFile)
    {
        var fileName = Path.GetFileName(sourceFile);
        var displayName = fileName;

        if (fileName.Length > 37
            && fileName[36] == '_'
            && Guid.TryParse(fileName[..36], out _))
        {
            displayName = fileName[37..];
        }

        return displayName.Replace("_", " ");
    }
}
