using ConflictDuplicationDetector.Core.Documents;
using ConflictDuplicationDetector.Core.Models;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Documents;

public class DocumentChunkerTests
{
    private readonly DocumentChunker _chunker = new();
    
    [Fact]
    public void ChunkDocument_EmptyContent_ReturnsNoChunks()
    {
        var document = new Document { Content = string.Empty };
        
        var chunks = _chunker.ChunkDocument(document).ToList();
        
        Assert.Empty(chunks);
    }
    
    [Fact]
    public void ChunkDocument_SmallContent_ReturnsSingleChunk()
    {
        var document = new Document 
        { 
            Id = "doc1",
            Content = "This is a small document.",
            FilePath = "/test/doc.txt"
        };
        
        var chunks = _chunker.ChunkDocument(document, chunkSize: 100).ToList();
        
        Assert.Single(chunks);
        Assert.Equal("doc1", chunks[0].DocumentId);
        Assert.Equal("This is a small document.", chunks[0].Content);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }
    
    [Fact]
    public void ChunkDocument_LargeContent_ReturnsMultipleChunks()
    {
        var content = string.Join(" ", Enumerable.Repeat("word", 200));
        var document = new Document 
        { 
            Id = "doc1",
            Content = content,
            FilePath = "/test/doc.txt"
        };
        
        var chunks = _chunker.ChunkDocument(document, chunkSize: 100, overlap: 20).ToList();
        
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.Equal("doc1", chunk.DocumentId));
        
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
        }
    }
    
    [Fact]
    public void ChunkDocument_GeneratesUniqueContentHashes()
    {
        var document = new Document 
        { 
            Content = "First chunk content. Second chunk content that is different."
        };
        
        var chunks = _chunker.ChunkDocument(document, chunkSize: 25, overlap: 0).ToList();
        
        var hashes = chunks.Select(c => c.ContentHash).ToList();
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }
    
    [Fact]
    public void ChunkDocument_PreservesSourceFile()
    {
        var document = new Document 
        { 
            Content = "Test content",
            FilePath = "/path/to/document.pdf"
        };
        
        var chunks = _chunker.ChunkDocument(document).ToList();
        
        Assert.All(chunks, chunk => Assert.Equal("/path/to/document.pdf", chunk.SourceFile));
    }
    
    [Fact]
    public void ChunkDocument_SetsCorrectOffsets()
    {
        var document = new Document 
        { 
            Content = "The quick brown fox jumps over the lazy dog."
        };
        
        var chunks = _chunker.ChunkDocument(document, chunkSize: 20, overlap: 0).ToList();
        
        Assert.Equal(0, chunks[0].StartOffset);
        Assert.True(chunks[0].EndOffset > chunks[0].StartOffset);
    }
}
