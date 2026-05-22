using System.Text;
using ConflictDuplicationDetector.Core.Models;
using HtmlAgilityPack;

namespace ConflictDuplicationDetector.Core.Documents;

public class HtmlDocumentParser : IDocumentParser
{
    private static readonly string[] SupportedExtensions = { ".html", ".htm", ".xhtml" };
    private static readonly HashSet<string> IgnoreTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "svg", "path", "meta", "link", "head"
    };
    
    public DocumentType SupportedType => DocumentType.Html;
    
    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    public async Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"HTML file not found: {filePath}", filePath);
            
        var htmlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var content = ExtractText(htmlContent);
        
        return new Document
        {
            FileName = Path.GetFileName(filePath),
            FilePath = Path.GetFullPath(filePath),
            Content = content,
            Type = DocumentType.Html,
            IngestedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["parser"] = "HtmlAgilityPack",
                ["extension"] = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".html"
            }
        };
    }
    
    public string ExtractText(string htmlContent)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);
        
        var contentBuilder = new StringBuilder();
        
        var title = doc.DocumentNode.SelectSingleNode("//title");
        if (title != null && !string.IsNullOrWhiteSpace(title.InnerText))
        {
            contentBuilder.AppendLine($"[Title] {HtmlEntity.DeEntitize(title.InnerText.Trim())}");
            contentBuilder.AppendLine();
        }
        
        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        ExtractTextFromNode(body, contentBuilder);
        
        return NormalizeWhitespace(contentBuilder.ToString());
    }
    
    private void ExtractTextFromNode(HtmlNode node, StringBuilder builder)
    {
        if (node == null) return;
        
        if (IgnoreTags.Contains(node.Name))
            return;
            
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append(text.Trim());
                builder.Append(" ");
            }
            return;
        }
        
        var isBlockElement = IsBlockElement(node.Name);
        
        if (isBlockElement && builder.Length > 0 && !builder.ToString().EndsWith(Environment.NewLine))
        {
            builder.AppendLine();
        }
        
        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                builder.Append($"[{node.Name.ToUpperInvariant()}] ");
                break;
            case "li":
                builder.Append("- ");
                break;
        }
        
        foreach (var child in node.ChildNodes)
        {
            ExtractTextFromNode(child, builder);
        }
        
        if (isBlockElement)
        {
            builder.AppendLine();
        }
    }
    
    private static bool IsBlockElement(string tagName)
    {
        return tagName.ToLowerInvariant() switch
        {
            "div" or "p" or "br" or "hr" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" 
            or "ul" or "ol" or "li" or "table" or "tr" or "td" or "th" or "thead" or "tbody"
            or "article" or "section" or "header" or "footer" or "nav" or "aside" 
            or "blockquote" or "pre" or "figure" or "figcaption" => true,
            _ => false
        };
    }
    
    private static string NormalizeWhitespace(string text)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var normalizedLines = lines
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"\s+", " "))
            .Where(line => !string.IsNullOrWhiteSpace(line));
            
        return string.Join(Environment.NewLine, normalizedLines);
    }
}
