using FluentValidation.AspNetCore;
using Scalar.AspNetCore;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Modules.Templates;
using MediaRankerServer.Modules.Media;
using MediaRankerServer.Modules.Reviews;
using MediaRankerServer.Modules.Files;
using MediaRankerServer.Modules.Test;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog;
using Amazon;
using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;

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

// Register Library Services.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddProblemDetailsHandling();
builder.Services.AddOpenApi();

// Configure DbContext.
builder.Services.AddDbContext<PostgreSQLContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));

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

// Register AWS Services.
var region = builder.Configuration["AWS:Region"];
var accessKey = builder.Configuration["AWS:AccessKey"];
var secretKey = builder.Configuration["AWS:SecretKey"];

var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Region = RegionEndpoint.GetBySystemName(region);
awsOptions.Credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);

builder.Services.AddDefaultAWSOptions(awsOptions);

builder.Services.AddAWSService<IAmazonS3>();

// Register Cognito authentication extension.
builder.Services.AddCognitoAuthentication(builder.Configuration);

// Register Module Services.
builder.Services.AddTemplatesModule();
builder.Services.AddMediaModule();
builder.Services.AddReviewsModule();
builder.Services.AddFilesModule(builder.Configuration, builder.Environment);
builder.Services.AddTestModule();

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

public partial class Program { }
