using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using Scalar.AspNetCore;
using finalyearproject.Services.Interfaces;
using finalyearproject.Services.Implementation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel / Form limits ────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = null;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
});

// ── JWT (must be BEFORE builder.Build()) ─────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First try cookie
            var accessToken = context.Request.Cookies["auth_token"];
            
            // If not in cookie, try query string (for SignalR)
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = context.Request.Query["access_token"];
            }
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

// ── Controllers & OpenAPI ────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.WriteIndented = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSecurityScheme>();
        
        document.Components.SecuritySchemes.Add("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        document.SecurityRequirements ??= new List<Microsoft.OpenApi.Models.OpenApiSecurityRequirement>();
        document.SecurityRequirements.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

        return Task.CompletedTask;
    });
});

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsers, UserServices>();
builder.Services.AddScoped<Imeeting, MeetingServices>();
builder.Services.AddScoped<IAudio, AudioServices>();
builder.Services.AddScoped<IStorage, BackblazeStorage>();
builder.Services.AddScoped<IAudioProcessingService, AudioProcessingService>();
builder.Services.AddHostedService<finalyearproject.Workers.TranscriptionWorker>();
builder.Services.AddHostedService<finalyearproject.Workers.MeetingSchedulerWorker>();
builder.Services.AddScoped<IDarajaService, DarajaService>();
// ── HTTP Clients ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IRecallAiService, RecallAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("Backblaze", c =>
{
    c.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHttpClient("Groq", c =>
{
    c.BaseAddress = new Uri("https://api.groq.com");
    c.Timeout     = TimeSpan.FromMinutes(10);
});

builder.Services.AddHttpClient<ITranscriber, AssemblyAITranscriptionService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(20);
});
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddHttpClient<Iminutes, MinutesServices>();
builder.Services.AddHttpClient<IBackblazeB2Service, BackblazeB2Service>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient<IDarajaService, DarajaService>();
builder.Services.AddHealthChecks();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // Allow your Next.js frontend
        policy.WithOrigins("http://localhost:3000","https://minutesapp.vercel.app") 
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ═══════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ═══════════════════════════════════════════════════════════════════════════════

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("finalyearproject")
               .WithTheme(ScalarTheme.Moon)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<finalyearproject.Hubs.NotificationHub>("/hubs/notifications");
app.Run();