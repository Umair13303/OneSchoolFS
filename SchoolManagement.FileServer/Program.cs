using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SchoolManagement.FileServer.Auth;
using FileServerOptions = SchoolManagement.FileServer.Configuration.FileServerOptions;
using SchoolManagement.FileServer.Data;
using SchoolManagement.FileServer.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Strongly-typed config ──────────────────────────────────────────────────
builder.Services.Configure<FileServerOptions>(
    builder.Configuration.GetSection(FileServerOptions.SectionName));

// ── Database (same SchoolDB as the Main API) ───────────────────────────────
builder.Services.AddDbContext<FileServerDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication: JWT (for users) + API key (for service-to-service) ─────
var jwtKey      = builder.Configuration["Jwt:Key"]!;
var jwtIssuer   = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(options =>
    {
        // Forward to whichever scheme matches the incoming request.
        options.DefaultScheme              = "MultiScheme";
        options.DefaultAuthenticateScheme  = "MultiScheme";
        options.DefaultChallengeScheme     = "MultiScheme";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { })
    .AddPolicyScheme("MultiScheme", "JWT or API Key", options =>
    {
        // If the request has X-Api-Key, dispatch to ApiKey; else fall back to JWT.
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
                ? ApiKeyAuthenticationHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
    });

builder.Services.AddAuthorization();

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// ── Upload size: lift the form limit; service does the real validation ─────
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = long.MaxValue;
    o.ValueLengthLimit         = int.MaxValue;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = long.MaxValue);

// ── CORS (Angular dev server) ──────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ── Controllers & Swagger ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "School Management — File Server API",
        Version     = "v1",
        Description = "Upload, download and manage files for the School Management System."
    });

    // JWT Bearer in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Paste the JWT issued by the Main API. Example: Bearer eyJhbGci..."
    });

    // X-Api-Key in Swagger
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name        = ApiKeyAuthenticationHandler.HeaderName,
        Type        = SecuritySchemeType.ApiKey,
        In          = ParameterLocation.Header,
        Description = "Shared secret for Main-API → FileServer calls (FileServer:ApiKey)."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ── Pipeline ───────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Server API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check — quick sanity ping that doesn't require auth.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "FileServer", time = DateTime.UtcNow }))
   .AllowAnonymous();

// This service has no UI of its own — send anyone who opens the bare root URL
// (e.g. checking it's alive in a browser) to the API docs instead of a 404.
app.MapGet("/", () => Results.Redirect("/swagger"))
   .AllowAnonymous();

app.Run();
