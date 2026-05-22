using ConflictDuplicationDetector.Core.Documents;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Documents;

public class DocumentParserFactoryTests
{
    private readonly DocumentParserFactory _factory = new();
    
    [Theory]
    [InlineData("/test/document.pdf")]
    [InlineData("/test/document.PDF")]
    public void GetParser_PdfFile_ReturnsPdfParser(string filePath)
    {
        var parser = _factory.GetParser(filePath);
        
        Assert.NotNull(parser);
        Assert.IsType<PdfDocumentParser>(parser);
    }
    
    [Theory]
    [InlineData("/test/document.docx")]
    [InlineData("/test/document.DOCX")]
    [InlineData("/test/document.docm")]
    public void GetParser_DocxFile_ReturnsDocxParser(string filePath)
    {
        var parser = _factory.GetParser(filePath);
        
        Assert.NotNull(parser);
        Assert.IsType<DocxDocumentParser>(parser);
    }
    
    [Theory]
    [InlineData("/test/document.html")]
    [InlineData("/test/document.htm")]
    [InlineData("/test/document.xhtml")]
    public void GetParser_HtmlFile_ReturnsHtmlParser(string filePath)
    {
        var parser = _factory.GetParser(filePath);
        
        Assert.NotNull(parser);
        Assert.IsType<HtmlDocumentParser>(parser);
    }
    
    [Theory]
    [InlineData("/test/document.txt")]
    [InlineData("/test/document.md")]
    public void GetParser_TextFile_ReturnsTextParser(string filePath)
    {
        var parser = _factory.GetParser(filePath);
        
        Assert.NotNull(parser);
        Assert.IsType<TextDocumentParser>(parser);
    }
    
    [Theory]
    [InlineData("/test/document.exe")]
    [InlineData("/test/document.dll")]
    [InlineData("/test/document.jpg")]
    public void GetParser_UnsupportedFile_ReturnsNull(string filePath)
    {
        var parser = _factory.GetParser(filePath);
        
        Assert.Null(parser);
    }
    
    [Fact]
    public void GetSupportedExtensions_ReturnsExpectedExtensions()
    {
        var extensions = _factory.GetSupportedExtensions().ToList();
        
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".docx", extensions);
        Assert.Contains(".html", extensions);
        Assert.Contains(".txt", extensions);
    }
}
