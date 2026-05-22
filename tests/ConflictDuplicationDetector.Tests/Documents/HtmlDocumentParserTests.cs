using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Documents;

public class HtmlDocumentParserTests
{
    private readonly HtmlDocumentParser _parser = new();
    
    [Theory]
    [InlineData(".html", true)]
    [InlineData(".htm", true)]
    [InlineData(".xhtml", true)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    [InlineData(".txt", false)]
    public void CanParse_ReturnsExpectedResult(string extension, bool expected)
    {
        var filePath = $"/test/document{extension}";
        
        var result = _parser.CanParse(filePath);
        
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void SupportedType_ReturnsHtml()
    {
        Assert.Equal(DocumentType.Html, _parser.SupportedType);
    }
    
    [Fact]
    public void ExtractText_BasicHtml_ExtractsContent()
    {
        var html = "<html><body><p>Hello World</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Hello World", result);
    }
    
    [Fact]
    public void ExtractText_WithTitle_IncludesTitle()
    {
        var html = "<html><head><title>Test Document</title></head><body><p>Content</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Test Document", result);
    }
    
    [Fact]
    public void ExtractText_IgnoresScriptTags()
    {
        var html = "<html><body><script>var x = 'secret';</script><p>Visible text</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Visible text", result);
        Assert.DoesNotContain("secret", result);
    }
    
    [Fact]
    public void ExtractText_IgnoresStyleTags()
    {
        var html = "<html><body><style>.class { color: red; }</style><p>Visible text</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Visible text", result);
        Assert.DoesNotContain("color", result);
    }
    
    [Fact]
    public void ExtractText_DecodesHtmlEntities()
    {
        var html = "<html><body><p>Price: &lt;$100 &amp; discount</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("<$100", result);
        Assert.Contains("&", result);
    }
    
    [Fact]
    public void ExtractText_HandlesHeadings()
    {
        var html = "<html><body><h1>Main Title</h1><h2>Subtitle</h2><p>Paragraph</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Main Title", result);
        Assert.Contains("Subtitle", result);
        Assert.Contains("Paragraph", result);
    }
    
    [Fact]
    public void ExtractText_HandlesLists()
    {
        var html = "<html><body><ul><li>Item 1</li><li>Item 2</li></ul></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.Contains("Item 1", result);
        Assert.Contains("Item 2", result);
    }
    
    [Fact]
    public void ExtractText_NormalizesWhitespace()
    {
        var html = "<html><body><p>Text   with    lots     of      spaces</p></body></html>";
        
        var result = _parser.ExtractText(html);
        
        Assert.DoesNotContain("  ", result);
    }
}
