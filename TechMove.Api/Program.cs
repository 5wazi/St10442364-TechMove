using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TechMove.Api.Data;
using TechMove.Api.Patterns.Factory;
using TechMove.Api.Patterns.Observer;
using TechMove.Api.Patterns.Repository;
using TechMove.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();

// ── Patterns ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IContractFactory, ContractFactory>();
builder.Services.AddScoped<IContractObserver, AuditLogObserver>();

// ── Business Services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IFileService, FileService>();

// ── HTTP Clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<IAuthService, AuthService>(c =>
    c.Timeout = TimeSpan.FromSeconds(10));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey     = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidateAudience         = true,
            ValidAudience            = jwtSection["Audience"],
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.FromMinutes(5)
        };

       
    });

builder.Services.AddAuthorization();

// ── CORS (allow MVC frontend) ─────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("FrontendPolicy", policy =>
    policy.WithOrigins(
              builder.Configuration["AllowedOrigins"] ?? "http://localhost:5000",
              "http://glms-frontend-web"             // Docker service name
          )
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TechMove Logistics API",
        Version = "v1",
        Description = "Backend API for the TechMove Logistics Management System. " +
                      "Authenticate via POST /api/auth/login using a Firebase ID token."
    });

    // Add JWT auth to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (obtained from POST /api/auth/login)."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Include XML doc comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechMove API v1");
    c.RoutePrefix = string.Empty; // Swagger at root /
});

app.UseCors("FrontendPolicy");

//app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Only migrate when NOT in Testing environment
var environment = app.Environment;
if (!environment.IsEnvironment("Testing") &&
    !environment.IsEnvironment("Test"))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider
                      .GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider
                          .GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Migration failed.");
    }
}

app.Run();

public partial class Program { }