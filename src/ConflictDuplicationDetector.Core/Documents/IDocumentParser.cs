using System.Text.RegularExpressions;
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
    private static readonly Regex PageMarkerRegex = new(@"\[Page\s+(\d+)\]", RegexOptions.Compiled);
    private static readonly Regex SectionMarkerRegex = new(@"\[Section\s+(\d+):\s*([^\]]+)\]", RegexOptions.Compiled);
    
    public IEnumerable<DocumentChunk> ChunkDocument(Document document, int chunkSize = 512, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(document.Content))
            yield break;
            
        var content = document.Content;
        var locationMap = BuildLocationMap(content);
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
                var location = GetLocationAtPosition(locationMap, position);
                
                var chunk = new DocumentChunk
                {
                    DocumentId = document.Id,
                    Content = chunkContent,
                    ChunkIndex = chunkIndex,
                    StartOffset = position,
                    EndOffset = endPosition,
                    SourceFile = document.FilePath,
                    ContentHash = ComputeHash(chunkContent),
                    PageNumber = location.PageNumber,
                    Section = location.Section
                };
                
                yield return chunk;
                chunkIndex++;
            }
            
            position = endPosition - overlap;
            if (position <= prevPosition || position < 0)
                position = endPosition;
        }
    }
    
    private static List<LocationMarker> BuildLocationMap(string content)
    {
        var markers = new List<LocationMarker>();
        
        foreach (Match match in PageMarkerRegex.Matches(content))
        {
            markers.Add(new LocationMarker
            {
                Position = match.Index,
                PageNumber = match.Groups[1].Value,
                Section = null
            });
        }
        
        foreach (Match match in SectionMarkerRegex.Matches(content))
        {
            markers.Add(new LocationMarker
            {
                Position = match.Index,
                PageNumber = null,
                Section = match.Groups[2].Value.Trim()
            });
        }
        
        markers.Sort((a, b) => a.Position.CompareTo(b.Position));
        
        return markers;
    }
    
    private static (string? PageNumber, string? Section) GetLocationAtPosition(List<LocationMarker> markers, int position)
    {
        string? pageNumber = null;
        string? section = null;
        
        foreach (var marker in markers)
        {
            if (marker.Position > position)
                break;
                
            if (marker.PageNumber != null)
                pageNumber = marker.PageNumber;
            if (marker.Section != null)
                section = marker.Section;
        }
        
        return (pageNumber, section);
    }
    
    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    private class LocationMarker
    {
        public int Position { get; set; }
        public string? PageNumber { get; set; }
        public string? Section { get; set; }
    }
}
