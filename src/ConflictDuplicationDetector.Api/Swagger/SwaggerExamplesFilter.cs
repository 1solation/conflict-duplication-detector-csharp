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

            case "IngestDocument":
                ConfigureFileUpload(operation);
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

    private static OpenApiObject HealthResponseExample => new()
    {
        ["status"] = new OpenApiString("healthy"),
        ["openAiConfigured"] = new OpenApiBoolean(true),
        ["knowledgeBaseExists"] = new OpenApiBoolean(true),
        ["chunkCount"] = new OpenApiInteger(142)
    };

    private static OpenApiObject KnowledgeBaseResponseExample => new()
    {
        ["exists"] = new OpenApiBoolean(true),
        ["chunkCount"] = new OpenApiInteger(142),
        ["persistPath"] = new OpenApiString("./data/vectors.json")
    };

    private static OpenApiObject IngestAcceptedExample => new()
    {
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
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Analysis"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiObject CheckAcceptedExample => new()
    {
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
        ["jobId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["type"] = new OpenApiString("Chat"),
        ["status"] = new OpenApiString("Pending"),
        ["statusUrl"] = new OpenApiString("/api/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6")
    };

    private static OpenApiArray ListJobsExample => new()
    {
        new OpenApiObject
        {
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
                    ["suggestedStandard"] = new OpenApiString("PTO (Paid Time Off)")
                }
            },
            ["metrics"] = new OpenApiObject
            {
                ["totalChunks"] = new OpenApiInteger(142),
                ["duplicationsFound"] = new OpenApiInteger(1),
                ["conflictsFound"] = new OpenApiInteger(1),
                ["inconsistenciesFound"] = new OpenApiInteger(1),
                ["totalDuration"] = new OpenApiString("00:00:14.523")
            }
        }
    };
}
