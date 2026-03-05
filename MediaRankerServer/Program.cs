using FluentValidation;
using FluentValidation.AspNetCore;
using Scalar.AspNetCore;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Rankings.Entities;
using MediaRankerServer.Shared.Extensions;
using MediaRankerServer.Modules.Templates.Contracts;
using MediaRankerServer.Modules.Templates;
using MediaRankerServer.Modules.Media;
using MediaRankerServer.Modules.Rankings;
using MediaRankerServer.Modules.Test;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddProblemDetailsHandling();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
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

// Register Module Services.
builder.Services.AddTemplatesModule();
builder.Services.AddMediaModule();
builder.Services.AddRankingsModule();
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
