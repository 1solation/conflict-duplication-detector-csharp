using ConflictDuplicationDetector.Core.Models;
using Xunit;

namespace ConflictDuplicationDetector.Tests.Models;

public class MetricsTrackerTests
{
    private readonly MetricsTracker _tracker = new();
    
    [Fact]
    public void StartCall_CreatesMetricWithAgentName()
    {
        var metric = _tracker.StartCall("TestAgent", "testOperation");
        
        Assert.Equal("TestAgent", metric.AgentName);
        Assert.Equal("testOperation", metric.Operation);
        Assert.NotEqual(default, metric.StartedAt);
    }
    
    [Fact]
    public void CompleteCall_RecordsMetrics()
    {
        var metric = _tracker.StartCall("TestAgent", "testOperation");
        
        _tracker.CompleteCall(metric, 
            inputTokens: 100, 
            outputTokens: 50, 
            networkTimeMs: 500, 
            calculationTimeMs: 100);
        
        Assert.Equal(100, metric.InputTokens);
        Assert.Equal(50, metric.OutputTokens);
        Assert.Equal(500, metric.NetworkTimeMs);
        Assert.Equal(100, metric.CalculationTimeMs);
        Assert.True(metric.Success);
        Assert.Null(metric.ErrorMessage);
    }
    
    [Fact]
    public void CompleteCall_RecordsErrors()
    {
        var metric = _tracker.StartCall("TestAgent", "testOperation");
        
        _tracker.CompleteCall(metric, success: false, error: "Test error");
        
        Assert.False(metric.Success);
        Assert.Equal("Test error", metric.ErrorMessage);
    }
    
    [Fact]
    public void GetAllMetrics_ReturnsAggregatedMetrics()
    {
        var metric1 = _tracker.StartCall("Agent1", "op1");
        _tracker.CompleteCall(metric1, inputTokens: 100, outputTokens: 50, networkTimeMs: 500, calculationTimeMs: 100);
        
        var metric2 = _tracker.StartCall("Agent1", "op2");
        _tracker.CompleteCall(metric2, inputTokens: 200, outputTokens: 100, networkTimeMs: 600, calculationTimeMs: 200);
        
        var metrics = _tracker.GetAllMetrics();
        
        Assert.Single(metrics);
        var agentMetrics = metrics[0];
        Assert.Equal("Agent1", agentMetrics.AgentName);
        Assert.Equal(2, agentMetrics.TotalCalls);
        Assert.Equal(300, agentMetrics.TotalInputTokens);
        Assert.Equal(150, agentMetrics.TotalOutputTokens);
        Assert.Equal(1100, agentMetrics.TotalNetworkTimeMs);
        Assert.Equal(300, agentMetrics.TotalCalculationTimeMs);
    }
    
    [Fact]
    public void GetAllMetrics_SeparatesAgents()
    {
        var metric1 = _tracker.StartCall("Agent1", "op1");
        _tracker.CompleteCall(metric1, inputTokens: 100, outputTokens: 50);
        
        var metric2 = _tracker.StartCall("Agent2", "op1");
        _tracker.CompleteCall(metric2, inputTokens: 200, outputTokens: 100);
        
        var metrics = _tracker.GetAllMetrics();
        
        Assert.Equal(2, metrics.Count);
        Assert.Contains(metrics, m => m.AgentName == "Agent1");
        Assert.Contains(metrics, m => m.AgentName == "Agent2");
    }
    
    [Fact]
    public void GetMetrics_ReturnsSpecificAgent()
    {
        var metric1 = _tracker.StartCall("Agent1", "op1");
        _tracker.CompleteCall(metric1);
        
        var metric2 = _tracker.StartCall("Agent2", "op1");
        _tracker.CompleteCall(metric2);
        
        var agent1Metrics = _tracker.GetMetrics("Agent1");
        
        Assert.NotNull(agent1Metrics);
        Assert.Equal("Agent1", agent1Metrics.AgentName);
    }
    
    [Fact]
    public void GetMetrics_NonExistentAgent_ReturnsNull()
    {
        var metrics = _tracker.GetMetrics("NonExistent");
        
        Assert.Null(metrics);
    }
    
    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        var metric = _tracker.StartCall("TestAgent", "op1");
        _tracker.CompleteCall(metric);
        
        _tracker.Reset();
        
        var metrics = _tracker.GetAllMetrics();
        Assert.Empty(metrics);
    }
    
    [Fact]
    public void AgentMetrics_AverageCalculations_Correct()
    {
        var metric1 = _tracker.StartCall("TestAgent", "op1");
        _tracker.CompleteCall(metric1, networkTimeMs: 100, calculationTimeMs: 50, inputTokens: 100, outputTokens: 50);
        
        var metric2 = _tracker.StartCall("TestAgent", "op2");
        _tracker.CompleteCall(metric2, networkTimeMs: 200, calculationTimeMs: 100, inputTokens: 200, outputTokens: 100);
        
        var agentMetrics = _tracker.GetMetrics("TestAgent");
        
        Assert.NotNull(agentMetrics);
        Assert.Equal(150, agentMetrics.AverageNetworkTimeMs);
        Assert.Equal(75, agentMetrics.AverageCalculationTimeMs);
        Assert.Equal(225, agentMetrics.AverageTokensPerCall);
    }
    
    [Fact]
    public void AgentCallMetric_TotalTimeMs_SumsCorrectly()
    {
        var metric = _tracker.StartCall("TestAgent", "op1");
        _tracker.CompleteCall(metric, networkTimeMs: 500, calculationTimeMs: 200);
        
        Assert.Equal(700, metric.TotalTimeMs);
    }
}
