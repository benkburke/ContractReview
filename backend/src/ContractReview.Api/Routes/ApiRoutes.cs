namespace ContractReview.Api.Routes;

public static class ApiRoutes
{
    public const string Health = "/health";
    public const string OpenApiDocument = "/openapi/v1.json";
    public const string SwaggerPrefix = "swagger";
    public const string HubsPrefix = "/hubs";
    public const string Contracts = "/api/contracts";
    public const string ContractById = "/{id:guid}";
    public const string ContractIndex = "/{id:guid}/index";
}
