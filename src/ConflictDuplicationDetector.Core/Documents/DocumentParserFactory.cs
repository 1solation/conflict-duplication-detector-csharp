using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Core.Documents;

public interface IDocumentParserFactory
{
    IDocumentParser? GetParser(string filePath);
    IEnumerable<string> GetSupportedExtensions();
}

public class DocumentParserFactory : IDocumentParserFactory
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    
    public DocumentParserFactory(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers;
    }
    
    public DocumentParserFactory() : this(GetDefaultParsers())
    {
    }
    
    public IDocumentParser? GetParser(string filePath)
    {
        return _parsers.FirstOrDefault(p => p.CanParse(filePath));
    }
    
    public IEnumerable<string> GetSupportedExtensions()
    {
        return new[] { ".pdf", ".docx", ".docm", ".html", ".htm", ".xhtml", ".txt" };
    }
    
    private static IEnumerable<IDocumentParser> GetDefaultParsers()
    {
        yield return new PdfDocumentParser();
        yield return new DocxDocumentParser();
        yield return new HtmlDocumentParser();
        yield return new TextDocumentParser();
    }
}

public class TextDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".txt", ".md", ".markdown" };
    
    public DocumentType SupportedType => DocumentType.Text;
    
    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    public async Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Text file not found: {filePath}", filePath);
            
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        
        return new Document
        {
            FileName = Path.GetFileName(filePath),
            FilePath = Path.GetFullPath(filePath),
            Content = content,
            Type = DocumentType.Text,
            IngestedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["parser"] = "TextParser",
                ["extension"] = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".txt"
            }
        };
    }
}
