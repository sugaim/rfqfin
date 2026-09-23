using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Rfq.Api;

public sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type sourceType = context.JsonTypeInfo.Type;
        Type? nullableType = Nullable.GetUnderlyingType(sourceType);
        Type enumType = nullableType ?? sourceType;
        if (!enumType.IsEnum)
        {
            return Task.CompletedTask;
        }

        schema.Type = "string";
        schema.Format = null;
        schema.Nullable = nullableType is not null;
        schema.Enum = [.. Enum.GetNames(enumType).Select(value => new OpenApiString(value))];
        return Task.CompletedTask;
    }
}
