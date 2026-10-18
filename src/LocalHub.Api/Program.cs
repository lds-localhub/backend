using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LocalHub.Api;
using LocalHub.Api.Json;
using LocalHub.Api.Middleware;
using LocalHub.Application;
using LocalHub.Application.Common;
using LocalHub.Infrastructure.Security;
using LocalHub.Infrastructure.Seed;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplication(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Enum values as in the API documentation (CLIENT, PROVIDER, ADMIN, ...) and instants in UTC.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Malformed bodies (invalid JSON, unknown enum value) and query parameters (for example ?page=abc)
        // use the same 400 format as the other errors.
        o.InvalidModelStateResponseFactory = context =>
        {
            // System.Text.Json reports the field as "$.role"; expose it as "role" with a Portuguese message.
            // A query parameter is reported with its own name ("page").
            var errors = new Dictionary<string, string[]>();
            foreach (var entry in context.ModelState)
            {
                if (entry.Value?.Errors.Count is null or 0)
                {
                    continue;
                }

                string? field = entry.Key.StartsWith("$.", StringComparison.Ordinal)
                    ? entry.Key.Substring(2)
                    : context.HttpContext.Request.Query.ContainsKey(entry.Key) ? entry.Key : null;
                if (field is not null)
                {
                    // [...] instead of new[] { ... }: the recommended .NET rule CA1861 rejects new[] here.
                    errors[field] = ["Valor inválido."];
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
        // Read here, when the API starts, so that the integration tests can replace the signing key.
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
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

// Every endpoint requires a session unless it is marked with [AllowAnonymous] (RF-F001-03).
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ErrorHandlingMiddleware>();
builder.Services.AddScoped<SessionValidationMiddleware>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health").AllowAnonymous();

await app.RunAsync();

public partial class Program { }