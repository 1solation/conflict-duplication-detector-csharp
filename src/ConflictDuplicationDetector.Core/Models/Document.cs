namespace ConflictDuplicationDetector.Core.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string? PageNumber { get; set; }
    public string? Section { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

public enum DocumentType
{
    Unknown,
    Pdf,
    Docx,
    Html,
    Text
}

public class DocumentMetadata
{
    public string DocumentId { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string? PageNumber { get; set; }
    public string? Section { get; set; }
}
