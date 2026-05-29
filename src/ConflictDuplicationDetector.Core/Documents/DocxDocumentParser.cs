using System.Text;
using ConflictDuplicationDetector.Core.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Document = ConflictDuplicationDetector.Core.Models.Document;
using DocumentType = ConflictDuplicationDetector.Core.Models.DocumentType;

namespace ConflictDuplicationDetector.Core.Documents;

public class DocxDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".docx", ".docm" };
    
    public DocumentType SupportedType => DocumentType.Docx;
    
    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    public async Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DOCX file not found: {filePath}", filePath);
            
        var content = await Task.Run(() => ExtractText(filePath), cancellationToken);
        
        return new Document
        {
            FileName = Path.GetFileName(filePath),
            FilePath = Path.GetFullPath(filePath),
            Content = content,
            Type = DocumentType.Docx,
            IngestedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["parser"] = "OpenXml",
                ["extension"] = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".docx"
            }
        };
    }
    
    private static string ExtractText(string filePath)
    {
        var contentBuilder = new StringBuilder();
        
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document.Body;
        var styles = document.MainDocumentPart?.StyleDefinitionsPart?.Styles;
        
        if (body == null)
            return string.Empty;
        
        var headingStyleIds = GetHeadingStyleIds(styles);
        var sectionNumber = 0;
            
        foreach (var element in body.Elements())
        {
            ProcessElement(element, contentBuilder, headingStyleIds, ref sectionNumber);
        }
        
        return contentBuilder.ToString().Trim();
    }
    
    private static HashSet<string> GetHeadingStyleIds(Styles? styles)
    {
        var headingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Heading1", "Heading2", "Heading3", "Heading4", "Heading5",
            "Title", "Subtitle", "TOCHeading"
        };
        
        if (styles != null)
        {
            foreach (var style in styles.Elements<Style>())
            {
                var styleName = style.StyleName?.Val?.Value;
                if (styleName != null && 
                    (styleName.Contains("Heading", StringComparison.OrdinalIgnoreCase) ||
                     styleName.Contains("Title", StringComparison.OrdinalIgnoreCase)))
                {
                    var styleId = style.StyleId?.Value;
                    if (styleId != null)
                        headingIds.Add(styleId);
                }
            }
        }
        
        return headingIds;
    }
    
    private static void ProcessElement(OpenXmlElement element, StringBuilder builder, HashSet<string> headingStyleIds, ref int sectionNumber)
    {
        switch (element)
        {
            case Paragraph paragraph:
                var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                var paragraphText = GetParagraphText(paragraph);
                
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    var isStyleHeading = styleId != null && headingStyleIds.Contains(styleId);
                    var isPatternHeading = !isStyleHeading && IsLikelyHeading(paragraph, paragraphText);
                    
                    if (isStyleHeading || isPatternHeading)
                    {
                        sectionNumber++;
                        builder.AppendLine($"[Section {sectionNumber}: {paragraphText.Trim()}]");
                    }
                    else
                    {
                        builder.AppendLine(paragraphText);
                    }
                }
                break;
                
            case Table table:
                ProcessTable(table, builder);
                break;
                
            default:
                foreach (var child in element.Elements())
                {
                    ProcessElement(child, builder, headingStyleIds, ref sectionNumber);
                }
                break;
        }
    }
    
    private static bool IsLikelyHeading(Paragraph paragraph, string text)
    {
        var trimmed = text.Trim();
        
        if (trimmed.Length < 3 || trimmed.Length > 100)
            return false;
        
        if (trimmed.EndsWith('.') || trimmed.EndsWith(',') || trimmed.EndsWith(';'))
            return false;
        
        var runProperties = paragraph.Elements<Run>().FirstOrDefault()?.RunProperties;
        if (runProperties != null)
        {
            var isBold = runProperties.Bold != null && (runProperties.Bold.Val == null || runProperties.Bold.Val.Value);
            var fontSize = runProperties.FontSize?.Val?.Value;
            var isLargeFont = fontSize != null && int.TryParse(fontSize, out var size) && size >= 28;
            
            if (isBold || isLargeFont)
                return true;
        }
        
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.Length <= 10)
        {
            var startsWithCapital = char.IsUpper(trimmed[0]);
            var noEndPunctuation = !trimmed.EndsWith('.') && !trimmed.EndsWith(',');
            var hasMostlyCapitalizedWords = words.Count(w => char.IsUpper(w[0])) >= words.Length * 0.6;
            
            if (startsWithCapital && noEndPunctuation && hasMostlyCapitalizedWords)
                return true;
        }
        
        return false;
    }
    
    private static string GetParagraphText(Paragraph paragraph)
    {
        var textBuilder = new StringBuilder();
        
        foreach (var run in paragraph.Elements<Run>())
        {
            foreach (var text in run.Elements<Text>())
            {
                textBuilder.Append(text.Text);
            }
        }
        
        return textBuilder.ToString();
    }
    
    private static void ProcessTable(Table table, StringBuilder builder)
    {
        builder.AppendLine("[Table]");
        
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<string>();
            
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellText = new StringBuilder();
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    cellText.Append(GetParagraphText(paragraph));
                    cellText.Append(" ");
                }
                cells.Add(cellText.ToString().Trim());
            }
            
            builder.AppendLine(string.Join(" | ", cells));
        }
        
        builder.AppendLine("[/Table]");
    }
}
