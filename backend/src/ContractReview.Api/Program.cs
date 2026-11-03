using ContractReview.Api.Cors;
using ContractReview.Api.Endpoints;
using ContractReview.Api.Hubs;
using ContractReview.Api.ExceptionHandling;
using ContractReview.Api.OpenApi;
using ContractReview.Api.Options;
using ContractReview.Api.Routes;
using ContractReview.Api.Http;
using ContractReview.Application;
using ContractReview.Application.Features.Claims.Progress;
using ContractReview.Application.Options;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<FrontendCorsOptions>()
    .BindConfiguration(FrontendCorsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SwaggerOptions>().BindConfiguration(SwaggerOptions.SectionName);

builder.Services.AddOptions<UploadOptions>()
    .BindConfiguration(UploadOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
var uploadOptions = builder.Configuration.GetSection(UploadOptions.SectionName).Get<UploadOptions>() ?? new UploadOptions();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = uploadOptions.MaxRequestBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = uploadOptions.MaxRequestBytes);

builder.Services.AddOptions<IndexingOptions>()
    .BindConfiguration(IndexingOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ClaimsOptions>()
    .BindConfiguration(ClaimsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ReviewOptions>()
    .BindConfiguration(ReviewOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IClaimProgressNotifier, SignalRClaimProgressNotifier>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidation();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.HttpContext.Response.StatusCode == StatusCodes.Status413PayloadTooLarge
        && !context.ProblemDetails.Extensions.ContainsKey(ProblemExtensions.Code))
    {
        context.ProblemDetails.Detail = ErrorMessages.ContractFileTooLarge;
        context.ProblemDetails.Extensions[ProblemExtensions.Code] = ErrorCodes.ContractFileTooLarge;
    }
});
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<EmbeddingFailedExceptionHandler>();
builder.Services.AddExceptionHandler<ClaimEvaluationFailedExceptionHandler>();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR().AddJsonProtocol(o =>
    o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi(ApiInfo.DocumentName, options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = ApiInfo.Title;
        document.Info.Version = ApiInfo.Version;
        document.Info.Description = ApiInfo.Description;
        return Task.CompletedTask;
    }).AddOperationTransformer(ContractOperationTransformer.TransformAsync)
    .AddSchemaTransformer(ProblemDetailsSchemaTransformer.TransformAsync));

var frontendOrigin = builder.Configuration
    .GetSection(FrontendCorsOptions.SectionName)
    .Get<FrontendCorsOptions>()?.FrontendOrigin ?? FrontendCorsOptions.DefaultFrontendOrigin;
builder.Services.AddCors(options => options.AddPolicy(CorsPolicies.Frontend, policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsPolicies.Frontend);

if (app.Configuration.GetSection(SwaggerOptions.SectionName).Get<SwaggerOptions>()?.Enabled == true)
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint(ApiRoutes.OpenApiDocument, ApiInfo.Title);
        o.RoutePrefix = ApiRoutes.SwaggerPrefix;
    });
}

app.MapHealthChecks(ApiRoutes.Health);
app.MapContractEndpoints();
app.MapHub<ClaimProgressHub>(ApiRoutes.ClaimProgressHub).RequireCors(CorsPolicies.Frontend);

app.Run();

public partial class Program;
