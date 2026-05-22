using System.Text;
using ConflictDuplicationDetector.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ConflictDuplicationDetector.Core.Documents;

public class PdfDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".pdf" };
    
    public DocumentType SupportedType => DocumentType.Pdf;
    
    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    public async Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}", filePath);
            
        var content = await Task.Run(() => ExtractText(filePath), cancellationToken);
        
        return new Document
        {
            FileName = Path.GetFileName(filePath),
            FilePath = Path.GetFullPath(filePath),
            Content = content,
            Type = DocumentType.Pdf,
            IngestedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["parser"] = "PdfPig",
                ["extension"] = ".pdf"
            }
        };
    }
    
    private static string ExtractText(string filePath)
    {
        var contentBuilder = new StringBuilder();
        
        using var document = PdfDocument.Open(filePath);
        
        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                contentBuilder.AppendLine($"[Page {page.Number}]");
                contentBuilder.AppendLine(pageText);
                contentBuilder.AppendLine();
            }
        }
        
        return contentBuilder.ToString().Trim();
    }
}
