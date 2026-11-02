using ContractReview.Api.Cors;
using ContractReview.Api.ExceptionHandling;
using ContractReview.Api.OpenApi;
using ContractReview.Api.Options;
using ContractReview.Api.Routes;
using ContractReview.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<FrontendCorsOptions>()
    .BindConfiguration(FrontendCorsOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SwaggerOptions>().BindConfiguration(SwaggerOptions.SectionName);

builder.Services.AddApplication();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi(ApiInfo.DocumentName, options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = ApiInfo.Title;
        document.Info.Version = ApiInfo.Version;
        document.Info.Description = ApiInfo.Description;
        return Task.CompletedTask;
    }));

var frontendOrigin = builder.Configuration
    .GetSection(FrontendCorsOptions.SectionName)
    .Get<FrontendCorsOptions>()?.FrontendOrigin ?? FrontendCorsOptions.DefaultFrontendOrigin;
builder.Services.AddCors(options => options.AddPolicy(CorsPolicies.Frontend, policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
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

app.Run();

public partial class Program;
