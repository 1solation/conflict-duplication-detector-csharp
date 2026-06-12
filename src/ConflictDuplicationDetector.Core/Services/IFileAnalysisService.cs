using ConflictDuplicationDetector.Core.Models;

namespace ConflictDuplicationDetector.Core.Services;

public interface IFileAnalysisService
{
    Task<AnalysisResult> AnalyseFileAsync(
        List<DocumentChunk> fileChunks,
        Document document,
        string analysisType = "all",
        CancellationToken cancellationToken = default);
}
