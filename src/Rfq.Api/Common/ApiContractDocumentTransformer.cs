using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Rfq.Api;

public sealed class ApiContractDocumentTransformer : IOpenApiDocumentTransformer
{
    private static readonly string[] ErrorStatuses = ["400", "403", "404", "409", "422", "500", "503"];

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas["ApiProblemDetails"] = CreateProblemSchema();
        foreach (OpenApiPathItem path in document.Paths.Values)
        {
            foreach (OpenApiOperation operation in path.Operations.Values)
            {
                foreach (string status in ErrorStatuses)
                {
                    operation.Responses.TryAdd(status, CreateProblemResponse());
                }
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiResponse CreateProblemResponse() => new()
    {
        Description = "Error",
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.Schema,
                        Id = "ApiProblemDetails",
                    },
                },
            },
        },
    };

    private static OpenApiSchema CreateProblemSchema() => new()
    {
        Type = "object",
        Required = new HashSet<string> { "status", "title", "detail", "code", "traceId" },
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["status"] = new() { Type = "integer", Format = "int32" },
            ["title"] = new() { Type = "string" },
            ["detail"] = new() { Type = "string" },
            ["code"] = new() { Type = "string" },
            ["traceId"] = new() { Type = "string" },
            ["errors"] = new()
            {
                Type = "object",
                AdditionalProperties = new OpenApiSchema
                {
                    Type = "array",
                    Items = new OpenApiSchema { Type = "string" },
                },
            },
            ["calculationErrorCode"] = new() { Type = "string", Nullable = true },
            ["failureLogId"] = new() { Type = "string", Format = "uuid", Nullable = true },
        },
    };
}
