using System.Text;
using AspNetCoreRateLimit;
using MexClub.Api.Middleware;
using MexClub.Application;
using MexClub.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Clean Architecture DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key must be configured in appsettings or User Secrets.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("Socio", policy => policy.RequireRole("socio", "admin"));
});

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("MexClubPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Anti-forgery / XSS
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

// Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var systemName = builder.Configuration["AppSettings:SystemName"] ?? "MexClub";
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"{systemName} API",
        Version = "v1",
        Description = $"API REST moderna para {systemName}"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Ejemplo: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MexClub API v1"));
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

// Servir archivos de recursos (fotos, documentos, firmas, estatutos) desde ruta configurable
var fileStorageBase = builder.Configuration["FileStorage:BasePath"] ?? "resources";
var fileStorageRequest = builder.Configuration["FileStorage:RequestPath"] ?? "/resources";
var resourcesRoot = Path.IsPathRooted(fileStorageBase)
    ? fileStorageBase
    : Path.Combine(Directory.GetCurrentDirectory(), fileStorageBase);

// Crear subcarpetas si no existen
foreach (var sub in new[] { "fotos", "documentos", "firmas", "estatutos" })
    Directory.CreateDirectory(Path.Combine(resourcesRoot, sub));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resourcesRoot),
    RequestPath = fileStorageRequest
});

app.UseCors("MexClubPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.UseIpRateLimiting();

app.MapControllers();

// Fallback for SPA
app.MapFallbackToFile("index.html");

app.Run();
