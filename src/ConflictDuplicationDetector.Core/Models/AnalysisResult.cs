namespace ConflictDuplicationDetector.Core.Models;

public class AnalysisResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime AnalysedAt { get; set; } = DateTime.UtcNow;
    public List<DuplicationResult> Duplications { get; set; } = new();
    public List<ConflictResult> Conflicts { get; set; } = new();
    public List<InconsistencyResult> Inconsistencies { get; set; } = new();
    public AnalysisMetrics Metrics { get; set; } = new();
}

public class DuplicationResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DuplicationType Type { get; set; }
    public double SimilarityScore { get; set; }
    public DocumentReference Source { get; set; } = new();
    public DocumentReference Target { get; set; } = new();
    public string SourceExcerpt { get; set; } = string.Empty;
    public string TargetExcerpt { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public enum DuplicationType
{
    Exact,
    Semantic,
    NearDuplicate
}

public class ConflictResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ConflictType Type { get; set; }
    public ConflictSeverity Severity { get; set; }
    public DocumentReference Source { get; set; } = new();
    public DocumentReference Target { get; set; } = new();
    public string SourceStatement { get; set; } = string.Empty;
    public string TargetStatement { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? Resolution { get; set; }
}

public enum ConflictType
{
    Contradiction,
    PolicyConflict,
    VersionMismatch,
    DataInconsistency
}

public enum ConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class InconsistencyResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public InconsistencyType Type { get; set; }
    public List<DocumentReference> Occurrences { get; set; } = new();
    public List<string> Variants { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public string? SuggestedStandard { get; set; }
}

public enum InconsistencyType
{
    Terminology,
    Formatting,
    Structure,
    DateFormat,
    Naming
}

public class DocumentReference
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ChunkId { get; set; } = string.Empty;
    public string? PageNumber { get; set; }
    public string? Section { get; set; }
    public int? LineNumber { get; set; }
}

public class AnalysisMetrics
{
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public int DuplicationsFound { get; set; }
    public int ConflictsFound { get; set; }
    public int InconsistenciesFound { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int TotalTokens => AgentMetrics.Sum(a => a.TotalTokens);
    public List<AgentMetrics> AgentMetrics { get; set; } = new();
}
