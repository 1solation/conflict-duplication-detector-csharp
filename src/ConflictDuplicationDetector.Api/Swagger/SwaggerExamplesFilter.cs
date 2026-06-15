using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConflictDuplicationDetector.Api.Swagger;

public class SwaggerExamplesFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var operationId = operation.OperationId;

        switch (operationId)
        {
            case "GetHealth":
                AddResponseExample(operation, "200", HealthResponseExample);
                break;

            case "GetKnowledgeBase":
                AddResponseExample(operation, "200", KnowledgeBaseResponseExample);
                break;

            case "IngestDocuments":
                ConfigureMultiFileUpload(operation);
                AddResponseExample(operation, "202", IngestAcceptedExample);
                break;

            case "StartAnalysis":
                AddRequestExample(operation, AnalysisRequestExample);
                AddResponseExample(operation, "202", AnalysisAcceptedExample);
                break;

            case "CheckDocument":
                ConfigureFileUpload(operation);
                AddResponseExample(operation, "202", CheckAcceptedExample);
                break;

            case "StartChat":
                AddRequestExample(operation, ChatRequestExample);
                AddResponseExample(operation, "202", ChatAcceptedExample);
                break;

            case "ListJobs":
                AddResponseArrayExample(operation, "200", ListJobsExample);
                break;

            case "GetJob":
                AddResponseExample(operation, "200", GetJobCompletedExample);
                break;
        }
    }

    private static void AddResponseExample(OpenApiOperation operation, string statusCode, OpenApiObject example)
    {
        if (operation.Responses.TryGetValue(statusCode, out var response) &&
            response.Content.TryGetValue("application/json", out var mediaType))
        {
            mediaType.Example = example;
        }
    }

    private static void AddResponseArrayExample(OpenApiOperation operation, string statusCode, OpenApiArray example)
    {
        if (operation.Responses.TryGetValue(statusCode, out var response) &&
            response.Content.TryGetValue("application/json", out var mediaType))
        {
            mediaType.Example = example;
        }
    }

    private static void AddRequestExample(OpenApiOperation operation, OpenApiObject example)
    {
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var mediaType) == true)
        {
            mediaType.Example = example;
        }
    }

    private static void ConfigureFileUpload(OpenApiOperation operation)
    {
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "Document file to upload. Supported formats: PDF, DOCX, HTML, TXT",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Required = new HashSet<string> { "file" },
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["file"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "The document file (PDF, DOCX, HTML, or TXT)"
                            }
                        }
                    }
                }
            }
        };
    }

    private static void ConfigureMultiFileUpload(OpenApiOperation operation)
    {
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "One or more document files to upload. Supported formats: PDF, DOCX, HTML, TXT",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Required = new HashSet<string> { "files" },
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["files"] = new OpenApiSchema
                            {
                                Type = "array",
                                Items = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                },
                                Description = "Document files (PDF, DOCX, HTML, or TXT)"
                            }
                        }
                    }
                }
            }
        };
    }

    private static OpenApiObject HealthResponseExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["status"] = new OpenApiString("healthy"),
        ["openAiConfigured"] = new OpenApiBoolean(true),
        ["knowledgeBaseExists"] = new OpenApiBoolean(true),
        ["chunkCount"] = new OpenApiInteger(142)
    };

    private static OpenApiObject KnowledgeBaseResponseExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["exists"] = new OpenApiBoolean(true),
        ["chunkCount"] = new OpenApiInteger(142),
        ["persistPath"] = new OpenApiString("./data/vectors.json")
    };

    private static OpenApiObject IngestAcceptedExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Ingest"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiObject AnalysisRequestExample => new()
    {
        ["type"] = new OpenApiString("all"),
        ["topic"] = new OpenApiNull()
    };

    private static OpenApiObject AnalysisAcceptedExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Analysis"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiObject CheckAcceptedExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Check"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiObject ChatRequestExample => new()
    {
        ["message"] = new OpenApiString("Find any duplicates related to the refund policy")
    };

    private static OpenApiObject ChatAcceptedExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Chat"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiArray ListJobsExample => new()
    {
        new OpenApiObject
        {
            ["provider"] = new OpenApiString("Azure OpenAI"),
            ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            ["type"] = new OpenApiString("Analysis"),
            ["status"] = new OpenApiString("Completed"),
            ["createdAt"] = new OpenApiString("2026-06-05T14:30:00Z"),
            ["startedAt"] = new OpenApiString("2026-06-05T14:30:01Z"),
            ["completedAt"] = new OpenApiString("2026-06-05T14:30:15Z"),
            ["error"] = new OpenApiNull(),
            ["result"] = new OpenApiNull()
        },
        new OpenApiObject
        {
            ["provider"] = new OpenApiString("Azure OpenAI"),
            ["jobId"] = new OpenApiString("7ca85f64-5717-4562-b3fc-2c963f66afa7"),
            ["type"] = new OpenApiString("Ingest"),
            ["status"] = new OpenApiString("Running"),
            ["createdAt"] = new OpenApiString("2026-06-05T14:32:00Z"),
            ["startedAt"] = new OpenApiString("2026-06-05T14:32:01Z"),
            ["completedAt"] = new OpenApiNull(),
            ["error"] = new OpenApiNull(),
            ["result"] = new OpenApiNull()
        }
    };

    private static OpenApiObject GetJobCompletedExample => new()
    {
        ["provider"] = new OpenApiString("Azure OpenAI"),
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Analysis"),
        ["status"] = new OpenApiString("Completed"),
        ["createdAt"] = new OpenApiString("2026-06-05T14:30:00Z"),
        ["startedAt"] = new OpenApiString("2026-06-05T14:30:01Z"),
        ["completedAt"] = new OpenApiString("2026-06-05T14:30:15Z"),
        ["error"] = new OpenApiNull(),
        ["result"] = new OpenApiObject
        {
            ["duplications"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["type"] = new OpenApiString("Semantic"),
                    ["similarityScore"] = new OpenApiDouble(0.92),
                    ["source"] = new OpenApiObject
                    {
                        ["fileName"] = new OpenApiString("policy-v1.pdf"),
                        ["pageNumber"] = new OpenApiString("3")
                    },
                    ["target"] = new OpenApiObject
                    {
                        ["fileName"] = new OpenApiString("handbook.docx"),
                        ["pageNumber"] = new OpenApiString("12")
                    },
                    ["sourceExcerpt"] = new OpenApiString("Employees must submit expense reports within 30 days..."),
                    ["targetExcerpt"] = new OpenApiString("All expense claims should be filed no later than one month..."),
                    ["explanation"] = new OpenApiString("Similarity score: 92.0%")
                }
            },
            ["conflicts"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["type"] = new OpenApiString("PolicyConflict"),
                    ["severity"] = new OpenApiString("High"),
                    ["source"] = new OpenApiObject
                    {
                        ["fileName"] = new OpenApiString("policy-2024.pdf")
                    },
                    ["target"] = new OpenApiObject
                    {
                        ["fileName"] = new OpenApiString("policy-2023.pdf")
                    },
                    ["sourceStatement"] = new OpenApiString("Remote work is permitted 3 days per week"),
                    ["targetStatement"] = new OpenApiString("All employees must work on-site 5 days per week"),
                    ["explanation"] = new OpenApiString("Direct contradiction in remote work policy"),
                    ["resolution"] = new OpenApiString("Retire the 2023 policy document")
                }
            },
            ["inconsistencies"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["type"] = new OpenApiString("Terminology"),
                    ["variants"] = new OpenApiArray
                    {
                        new OpenApiString("PTO"),
                        new OpenApiString("Paid Time Off"),
                        new OpenApiString("Annual Leave")
                    },
                    ["occurrences"] = new OpenApiArray
                    {
                        new OpenApiObject { ["fileName"] = new OpenApiString("hr-guide.pdf") },
                        new OpenApiObject { ["fileName"] = new OpenApiString("employee-handbook.docx") }
                    },
                    ["explanation"] = new OpenApiString("Same concept referred to by different names"),
                    ["suggestion"] = new OpenApiString("PTO (Paid Time Off)")
                }
            },
            ["metrics"] = new OpenApiObject
            {
                ["totalDocuments"] = new OpenApiInteger(3),
                ["totalChunks"] = new OpenApiInteger(31),
                ["duplicationsFound"] = new OpenApiInteger(1),
                ["conflictsFound"] = new OpenApiInteger(1),
                ["inconsistenciesFound"] = new OpenApiInteger(4),
                ["totalDuration"] = new OpenApiString("00:00:14.1087310"),
                ["totalTokens"] = new OpenApiInteger(4989),
                ["agentMetrics"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["agentName"] = new OpenApiString("ConflictAgent"),
                        ["totalCalls"] = new OpenApiInteger(1),
                        ["totalNetworkTimeMs"] = new OpenApiLong(12593),
                        ["totalCalculationTimeMs"] = new OpenApiLong(254),
                        ["totalInputTokens"] = new OpenApiInteger(2189),
                        ["totalOutputTokens"] = new OpenApiInteger(298),
                        ["totalTokens"] = new OpenApiInteger(2487),
                        ["callMetrics"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["callId"] = new OpenApiString("ad7cc708-4596-48f2-bd72-faa744e252ab"),
                                ["agentName"] = new OpenApiString("ConflictAgent"),
                                ["operation"] = new OpenApiString("invoke"),
                                ["startedAt"] = new OpenApiString("2026-06-08T13:47:12.745764Z"),
                                ["completedAt"] = new OpenApiString("2026-06-08T13:47:25.598582Z"),
                                ["networkTimeMs"] = new OpenApiLong(12593),
                                ["calculationTimeMs"] = new OpenApiLong(254),
                                ["inputTokens"] = new OpenApiInteger(2189),
                                ["outputTokens"] = new OpenApiInteger(298),
                                ["success"] = new OpenApiBoolean(true),
                                ["errorMessage"] = new OpenApiNull(),
                                ["totalTimeMs"] = new OpenApiLong(12847)
                            }
                        },
                        ["averageNetworkTimeMs"] = new OpenApiDouble(12593),
                        ["averageCalculationTimeMs"] = new OpenApiDouble(254),
                        ["averageTokensPerCall"] = new OpenApiDouble(2487)
                    },
                    new OpenApiObject
                    {
                        ["agentName"] = new OpenApiString("InconsistencyAgent"),
                        ["totalCalls"] = new OpenApiInteger(1),
                        ["totalNetworkTimeMs"] = new OpenApiLong(13840),
                        ["totalCalculationTimeMs"] = new OpenApiLong(246),
                        ["totalInputTokens"] = new OpenApiInteger(1785),
                        ["totalOutputTokens"] = new OpenApiInteger(717),
                        ["totalTokens"] = new OpenApiInteger(2502),
                        ["callMetrics"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["callId"] = new OpenApiString("9f40986a-a02d-49d5-8356-e16994dfd529"),
                                ["agentName"] = new OpenApiString("InconsistencyAgent"),
                                ["operation"] = new OpenApiString("invoke"),
                                ["startedAt"] = new OpenApiString("2026-06-08T13:47:12.7472Z"),
                                ["completedAt"] = new OpenApiString("2026-06-08T13:47:26.836825Z"),
                                ["networkTimeMs"] = new OpenApiLong(13840),
                                ["calculationTimeMs"] = new OpenApiLong(246),
                                ["inputTokens"] = new OpenApiInteger(1785),
                                ["outputTokens"] = new OpenApiInteger(717),
                                ["success"] = new OpenApiBoolean(true),
                                ["errorMessage"] = new OpenApiNull(),
                                ["totalTimeMs"] = new OpenApiLong(14086)
                            }
                        },
                        ["averageNetworkTimeMs"] = new OpenApiDouble(13840),
                        ["averageCalculationTimeMs"] = new OpenApiDouble(246),
                        ["averageTokensPerCall"] = new OpenApiDouble(2502)
                    }
                }
            }
        }
    };
}
