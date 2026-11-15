using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LocalHub.Api;
using LocalHub.Api.Filters;
using LocalHub.Api.Json;
using LocalHub.Api.Middleware;
using LocalHub.Application;
using LocalHub.Application.Common;
using LocalHub.Infrastructure.Security;
using LocalHub.Infrastructure.Seed;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplication(builder.Configuration);

const string InvalidValueMessage = "Valor inválido.";

builder.Services.AddControllers(o =>
    {
        // Portuguese message for query parameters that cannot be converted (e.g. ?page=abc).
        o.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, _) => InvalidValueMessage);
        o.ModelBindingMessageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => InvalidValueMessage);
        o.ModelBindingMessageProvider.SetUnknownValueIsInvalidAccessor(_ => InvalidValueMessage);
        o.ModelBindingMessageProvider.SetValueIsInvalidAccessor(_ => InvalidValueMessage);
        o.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(_ => InvalidValueMessage);
    })
    .AddJsonOptions(o =>
    {
        // Enums only as the documented text (e.g. "WEB"): numbers such as 1 or 99 are rejected with 400.
        o.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Same 400 format for DTO validation errors, malformed JSON and invalid query parameters.
        o.InvalidModelStateResponseFactory = context =>
        {
            var bodyParameters = context.ActionDescriptor.Parameters
                .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
                .Select(p => p.Name)
                .ToList();

            var errors = new Dictionary<string, string[]>();
            foreach (var entry in context.ModelState)
            {
                if (entry.Value == null || entry.Value.Errors.Count == 0)
                {
                    continue;
                }

                if (entry.Key.StartsWith("$.", StringComparison.Ordinal))
                {
                    // System.Text.Json reports the field as "$.role".
                    errors[entry.Key.Substring(2)] = new[] { InvalidValueMessage };
                }
                else if (entry.Key.Length > 0 && entry.Key != "$" && !bodyParameters.Contains(entry.Key))
                {
                    errors[JsonNamingPolicy.CamelCase.ConvertName(entry.Key)] = entry.Value.Errors
                        .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? InvalidValueMessage : e.ErrorMessage)
                        .ToArray();
                }
            }

            var problem = new HttpValidationProblemDetails(errors);
            ProblemResponses.Fill(
                problem,
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Dados inválidos",
                errors.Count > 0
                    ? "Corrija os campos assinalados."
                    : "O corpo do pedido está em falta ou não é um JSON válido.",
                type: null);
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Read at startup so that the integration tests can replace the signing key.
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Only this API issues and validates the tokens.
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ProblemResponses.WriteSessionInvalidAsync(context.HttpContext);
            },
            OnForbidden = context => ProblemResponses.WriteForbiddenAsync(context.HttpContext),
        };
    });

// Origins of the web frontends that may call the API from the browser (Cors:AllowedOrigins). Empty by default:
// in development the Vite proxy makes the requests same-origin, and each deployment sets its own origins.
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Every endpoint requires a session unless it is marked with [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ErrorHandlingMiddleware>();
builder.Services.AddScoped<SessionValidationMiddleware>();
builder.Services.AddScoped<RequireProfessionalProfileFilter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
// 404 and 405 from routing have no body: they get the same problem+json format as the other errors.
app.UseStatusCodePages(context => ProblemResponses.WriteUnmatchedRouteAsync(context.HttpContext));
// Before authentication: the preflight (OPTIONS) has no token and must not get a 401.
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

await app.RunAsync();

public partial class Program { }