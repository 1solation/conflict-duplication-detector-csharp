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
        
        if (body == null)
            return string.Empty;
            
        foreach (var element in body.Elements())
        {
            ProcessElement(element, contentBuilder);
        }
        
        return contentBuilder.ToString().Trim();
    }
    
    private static void ProcessElement(OpenXmlElement element, StringBuilder builder)
    {
        switch (element)
        {
            case Paragraph paragraph:
                var paragraphText = GetParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    builder.AppendLine(paragraphText);
                }
                break;
                
            case Table table:
                ProcessTable(table, builder);
                break;
                
            default:
                foreach (var child in element.Elements())
                {
                    ProcessElement(child, builder);
                }
                break;
        }
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
