using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Core.Documents;

public interface IDocumentParser
{
    bool CanParse(string filePath);
    Task<Document> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    DocumentType SupportedType { get; }
}

public interface IDocumentChunker
{
    IEnumerable<DocumentChunk> ChunkDocument(Document document, int chunkSize = 512, int overlap = 50);
}

public class DocumentChunker : IDocumentChunker
{
    public IEnumerable<DocumentChunk> ChunkDocument(Document document, int chunkSize = 512, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(document.Content))
            yield break;
            
        var content = document.Content;
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;
        var position = 0;
        
        while (position < content.Length)
        {
            var prevPosition = position;
            var endPosition = Math.Min(position + chunkSize, content.Length);
            
            if (endPosition < content.Length)
            {
                var lastSpace = content.LastIndexOf(' ', endPosition, Math.Min(endPosition - position, 100));
                if (lastSpace > position)
                    endPosition = lastSpace;
            }
            
            var chunkContent = content.Substring(position, endPosition - position).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunkContent))
            {
                var chunk = new DocumentChunk
                {
                    DocumentId = document.Id,
                    Content = chunkContent,
                    ChunkIndex = chunkIndex,
                    StartOffset = position,
                    EndOffset = endPosition,
                    SourceFile = document.FilePath,
                    ContentHash = ComputeHash(chunkContent)
                };
                
                yield return chunk;
                chunkIndex++;
            }
            
            position = endPosition - overlap;
            if (position <= prevPosition || position < 0)
                position = endPosition;
        }
    }
    
    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
