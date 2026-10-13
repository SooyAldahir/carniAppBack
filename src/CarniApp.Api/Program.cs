using System.Text;
using CarniApp.Api.Security;
using CarniApp.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Infraestructura (EF Core + SQL Server + Repository/UnitOfWork, D-04) ---
builder.Services.AddInfrastructure(builder.Configuration);

// --- CORS: Flutter corre en otro origen/puerto (web, desktop, movil) ---
// Permisivo en desarrollo; se restringe a dominios reales antes de produccion.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterClients", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

// --- Autenticacion JWT + Refresh Tokens (RF-AUTH-002, RNF-SEC-003) ---
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key en appsettings.json");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// --- Autorizacion por permiso granular (RF-AUTH-004, SUP-03) ---
// PermisoPolicyProvider resuelve policies "Permiso:<clave>" dinamicamente,
// sin tener que registrar cada permiso del catalogo a mano aqui.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CarniApp API",
        Version = "v1",
        Description = "Backend del sistema de administracion para carnicerias."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Pega el access token (sin la palabra 'Bearer')."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FlutterClients");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
