namespace ConflictDuplicationDetector.Api.Models;

public static class ChatResponseDisplayFormatter
{
    private const string NoConflictingDocumentFound = "No conflicting document found";
    private const string NoConflictingStatementFound = "No conflicting statement found in provided knowledge base.";

    public static IReadOnlyList<string> FormatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return [];
        }

        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();
        var lines = message.Replace("\r\n", "\n").Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                AddCurrentParagraph(paragraphs, currentParagraph);
                continue;
            }

            if (line.StartsWith("**", StringComparison.Ordinal))
            {
                AddCurrentParagraph(paragraphs, currentParagraph);
                currentParagraph.Add(FormatHeading(line));
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                var formattedBullet = FormatBullet(line[2..]);
                if (!string.IsNullOrWhiteSpace(formattedBullet))
                {
                    currentParagraph.Add(formattedBullet);
                }

                continue;
            }

            AddCurrentParagraph(paragraphs, currentParagraph);
            paragraphs.Add(StripMarkdown(line));
        }

        AddCurrentParagraph(paragraphs, currentParagraph);
        return paragraphs;
    }

    private static void AddCurrentParagraph(List<string> paragraphs, List<string> currentParagraph)
    {
        if (currentParagraph.Count == 0)
        {
            return;
        }

        paragraphs.Add(string.Join(" ", currentParagraph));
        currentParagraph.Clear();
    }

    private static string FormatHeading(string line)
    {
        var withoutMarkdown = StripMarkdown(line);
        var detailStart = withoutMarkdown.IndexOf(" (", StringComparison.Ordinal);

        if (detailStart < 0)
        {
            return $"{AddSpacesBeforeUppercase(withoutMarkdown)}.";
        }

        var title = withoutMarkdown[..detailStart];
        var detail = withoutMarkdown[detailStart..].Trim();
        return $"{AddSpacesBeforeUppercase(title)} {detail}.";
    }

    private static string FormatBullet(string line)
    {
        var labelEnd = line.IndexOf(':');
        if (labelEnd < 0)
        {
            return StripMarkdown(line);
        }

        var label = line[..labelEnd].Trim();
        var value = StripMarkdown(line[(labelEnd + 1)..].Trim());

        return label.ToLowerInvariant() switch
        {
            "source" => FormatDocumentStatement("Source", value),
            "target" => FormatDocumentStatement("Target", value),
            "explanation" => $"Explanation: {value}",
            "suggested resolution" => $"Suggested resolution: {value}",
            "suggested standard" => $"Suggested standard: {value}",
            _ => $"{label}: {value}"
        };
    }

    private static string FormatDocumentStatement(string label, string value)
    {
        var documentName = value;
        string? statement = null;
        var statementSeparator = value.IndexOf(" - \"", StringComparison.Ordinal);

        if (statementSeparator >= 0)
        {
            documentName = value[..statementSeparator].Trim();
            statement = value[(statementSeparator + 4)..].Trim().Trim('"');
        }

        documentName = AnalysisResultDisplayFormatter.GetDisplayDocumentName(documentName);
        if (string.Equals(documentName, NoConflictingDocumentFound, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(statement)
            || string.Equals(statement.Trim(), NoConflictingStatementFound, StringComparison.OrdinalIgnoreCase))
        {
            return $"{label} document: {documentName}.";
        }

        return $"{label} document: {documentName}, \"{statement}\".";
    }

    private static string StripMarkdown(string value) =>
        value.Replace("**", string.Empty).Trim().TrimStart('#').Trim();

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
