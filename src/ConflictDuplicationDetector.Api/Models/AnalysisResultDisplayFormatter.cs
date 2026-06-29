using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Api.Models;

public static class AnalysisResultDisplayFormatter
{
    private const string NoConflictingDocumentFound = "No conflicting document found";

    public static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static string FormatEnumName(Enum value) =>
        AddSpacesBeforeUppercase(value.ToString());

    public static bool ShouldDisplayDocumentReference(DocumentReference document) =>
        !string.Equals(
            GetDisplayDocumentName(document.FileName),
            NoConflictingDocumentFound,
            StringComparison.OrdinalIgnoreCase);

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

    private static string AddSpacesBeforeUppercase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var displayName = new System.Text.StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (index > 0 && char.IsUpper(character))
            {
                displayName.Append(' ');
            }

            displayName.Append(character);
        }

        return displayName.ToString();
    }
}
