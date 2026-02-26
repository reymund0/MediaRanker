using FluentValidation;
using FluentValidation.AspNetCore;
using Scalar.AspNetCore;
using MediaRankerServer.Data.Entities;
using MediaRankerServer.Models;
using MediaRankerServer.Models.Templates;
using MediaRankerServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/app-log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            shared: true
        );
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Default to requiring authentication for all controllers.
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<TemplateUpsertRequestValidator>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var httpContext = context.HttpContext;
        var problemDetails = context.ProblemDetails;
        var exception = context.Exception
            ?? httpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

        problemDetails.Instance = httpContext.Request.Path;

        if (exception is DomainException domainException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Type = domainException.Type;
            problemDetails.Title = "Domain error";
            problemDetails.Detail = domainException.Message;
            return;
        }

        var errorId = Guid.NewGuid().ToString();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        problemDetails.Status = StatusCodes.Status500InternalServerError;
        problemDetails.Type = "unexpected_error";
        problemDetails.Title = "Unexpected error occurred";
        problemDetails.Detail = $"Unexpected error occurred. Report this error code to your IT department: {errorId}";
        problemDetails.Extensions["errorId"] = errorId;

        logger.LogError(
            exception,
            "Unhandled exception. ErrorId: {ErrorId}, TraceId: {TraceId}",
            errorId,
            httpContext.TraceIdentifier
        );
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Configure DbContext.
builder.Services.AddDbContext<PostgreSQLContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => {
            npgsqlOptions.MapEnum<MediaType>("media_type");
        }
    );

    options.UseSnakeCaseNamingConvention();
});

// Add CORS support.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Cognito authentication middleware.
var region = builder.Configuration["AWS:Region"];
var userPoolId = builder.Configuration["AWS:CognitoUserPoolId"];
var clientId = builder.Configuration["AWS:CognitoClientId"];
var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = true; // IDK if I'll need this.

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,

            ValidateAudience = true,
            ValidAudience = clientId,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5), // Adjust as needed for token expiration tolerance
            NameClaimType = "sub"
        };
    });

// Register services.
builder.Services.AddScoped<ITemplatesService, TemplatesService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    // Map OpenAPI endpoint (optional, but good practice for development)
    app.MapOpenApi();
    // Map Scalar API Reference
    app.MapScalarApiReference();
    // Reroute requests to "/" to scalar docs.
    app.MapGet("/", context =>
    {
        context.Response.Redirect("/scalar/v1");
        return Task.CompletedTask;
    });
} else {
    // Force HTTPS in any environment other than development.
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowFrontend");

app.MapControllers();

app.Run();
