using ContractReview.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ContractReview.Api.OpenApi;

/// <summary>Declares the <c>code</c> extension that every ProblemDetails response carries.</summary>
public static class ProblemDetailsSchemaTransformer
{
    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties[ProblemExtensions.Code] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
            };
        }

        return Task.CompletedTask;
    }
}
