using ConflictDuplicationDetector.Core.Models;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Models;

public class AnalysisResultTests
{
    [Fact]
    public void AnalysisResult_InitializesWithEmptyLists()
    {
        var result = new AnalysisResult();

        Assert.NotNull(result.Duplications);
        Assert.NotNull(result.Conflicts);
        Assert.NotNull(result.Inconsistencies);
        Assert.Empty(result.Duplications);
        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Inconsistencies);
    }

    [Fact]
    public void AnalysisResult_GeneratesId()
    {
        var result = new AnalysisResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public void AnalysisResult_SetsAnalysedAt()
    {
        var before = DateTime.UtcNow;
        var result = new AnalysisResult();
        var after = DateTime.UtcNow;

        Assert.InRange(result.AnalysedAt, before, after);
    }

    [Fact]
    public void DuplicationResult_GeneratesId()
    {
        var result = new DuplicationResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public void ConflictResult_GeneratesId()
    {
        var result = new ConflictResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public void InconsistencyResult_GeneratesId()
    {
        var result = new InconsistencyResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
    }

    [Fact]
    public void InconsistencyResult_InitializesWithEmptyLists()
    {
        var result = new InconsistencyResult();

        Assert.NotNull(result.Variants);
        Assert.NotNull(result.Occurrences);
        Assert.Empty(result.Variants);
        Assert.Empty(result.Occurrences);
    }

    [Fact]
    public void DocumentReference_HasDefaultValues()
    {
        var reference = new DocumentReference();

        Assert.Equal(string.Empty, reference.DocumentId);
        Assert.Equal(string.Empty, reference.FileName);
        Assert.Equal(string.Empty, reference.ChunkId);
        Assert.Null(reference.PageNumber);
        Assert.Null(reference.Section);
        Assert.Null(reference.LineNumber);
    }

    [Fact]
    public void AgentMetrics_TotalTokens_SumsInputAndOutput()
    {
        var metrics = new AgentMetrics
        {
            TotalInputTokens = 100,
            TotalOutputTokens = 50
        };

        Assert.Equal(150, metrics.TotalTokens);
    }

    [Fact]
    public void AgentMetrics_AverageCalculations_HandleZeroCalls()
    {
        var metrics = new AgentMetrics { TotalCalls = 0 };

        Assert.Equal(0, metrics.AverageNetworkTimeMs);
        Assert.Equal(0, metrics.AverageCalculationTimeMs);
        Assert.Equal(0, metrics.AverageTokensPerCall);
    }
}
