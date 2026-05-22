namespace ConflictDuplicationDetector.Core.Models;

public class AgentMetrics
{
    public string AgentName { get; set; } = string.Empty;
    public int TotalCalls { get; set; }
    public long TotalNetworkTimeMs { get; set; }
    public long TotalCalculationTimeMs { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens => TotalInputTokens + TotalOutputTokens;
    public List<AgentCallMetric> CallMetrics { get; set; } = new();
    
    public double AverageNetworkTimeMs => TotalCalls > 0 ? (double)TotalNetworkTimeMs / TotalCalls : 0;
    public double AverageCalculationTimeMs => TotalCalls > 0 ? (double)TotalCalculationTimeMs / TotalCalls : 0;
    public double AverageTokensPerCall => TotalCalls > 0 ? (double)TotalTokens / TotalCalls : 0;
}

public class AgentCallMetric
{
    public string CallId { get; set; } = Guid.NewGuid().ToString();
    public string AgentName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public long NetworkTimeMs { get; set; }
    public long CalculationTimeMs { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public long TotalTimeMs => NetworkTimeMs + CalculationTimeMs;
}

public class MetricsTracker
{
    private readonly Dictionary<string, AgentMetrics> _agentMetrics = new();
    private readonly object _lock = new();
    
    public AgentCallMetric StartCall(string agentName, string operation)
    {
        return new AgentCallMetric
        {
            AgentName = agentName,
            Operation = operation,
            StartedAt = DateTime.UtcNow
        };
    }
    
    public void CompleteCall(AgentCallMetric metric, int inputTokens = 0, int outputTokens = 0, 
        long networkTimeMs = 0, long calculationTimeMs = 0, bool success = true, string? error = null)
    {
        metric.CompletedAt = DateTime.UtcNow;
        metric.InputTokens = inputTokens;
        metric.OutputTokens = outputTokens;
        metric.NetworkTimeMs = networkTimeMs;
        metric.CalculationTimeMs = calculationTimeMs;
        metric.Success = success;
        metric.ErrorMessage = error;
        
        lock (_lock)
        {
            if (!_agentMetrics.TryGetValue(metric.AgentName, out var agentMetrics))
            {
                agentMetrics = new AgentMetrics { AgentName = metric.AgentName };
                _agentMetrics[metric.AgentName] = agentMetrics;
            }
            
            agentMetrics.TotalCalls++;
            agentMetrics.TotalNetworkTimeMs += networkTimeMs;
            agentMetrics.TotalCalculationTimeMs += calculationTimeMs;
            agentMetrics.TotalInputTokens += inputTokens;
            agentMetrics.TotalOutputTokens += outputTokens;
            agentMetrics.CallMetrics.Add(metric);
        }
    }
    
    public List<AgentMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return _agentMetrics.Values.ToList();
        }
    }
    
    public AgentMetrics? GetMetrics(string agentName)
    {
        lock (_lock)
        {
            return _agentMetrics.GetValueOrDefault(agentName);
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _agentMetrics.Clear();
        }
    }
}
