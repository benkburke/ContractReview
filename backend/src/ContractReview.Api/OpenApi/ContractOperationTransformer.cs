using ContractReview.Api.Endpoints;
using ContractReview.Api.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ContractReview.Api.OpenApi;

public static class ContractOperationTransformer
{
    public static Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var names = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EndpointNameMetadata>()
            .Select(m => m.EndpointName)
            .ToList();
        var isUpload = names.Contains(ContractEndpoints.UploadContractName);
        if (!isUpload && !names.Contains(ContractEndpoints.EvaluateClaimName))
        {
            return Task.CompletedTask;
        }

        if (isUpload)
        {
            operation.RequestBody?.Content?.Remove(HeaderNames.FormUrlEncoded);
        }

        if (operation.Responses?.TryGetValue(StatusCodes.Status201Created.ToString(), out var created) == true
            && created is OpenApiResponse response)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[HeaderNames.Location] = new OpenApiHeader
            {
                Description = HeaderNames.LocationDescription,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }

        return Task.CompletedTask;
    }
}
