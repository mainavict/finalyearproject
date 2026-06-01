using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using Scalar.AspNetCore;
using finalyearproject.Services.Interfaces;
using finalyearproject.Services.Implementation;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();



// 1. Registers the .NET 9 OpenAPI generator
builder.Services.AddOpenApi(); 

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IUsers, UserServices>();
builder.Services.AddScoped<Imeeting, MeetingServices>();
builder.Services.AddScoped<IAudio, AudioServices>();
builder.Services.AddScoped<IStorage,BackblazeStorage>();
builder.Services.AddHttpClient<IRecallAiService, RecallAiService>();
builder.Services.AddHttpClient();

builder.Services.AddHttpClient("Backblaze", c =>
{
    c.Timeout = TimeSpan.FromMinutes(10);  // large files take time
});

// Client for Groq API
builder.Services.AddHttpClient("Groq", c =>
{
    c.BaseAddress = new Uri("https://api.groq.com");
    c.Timeout     = TimeSpan.FromMinutes(10);
});

// Register transcription service
builder.Services.AddHttpClient<Iminutes, MinutesServices>();
// Program.cs
builder.Services.AddHttpClient<IBackblazeB2Service, BackblazeB2Service>();
builder.Services.AddHttpClient<ITranscriber, GroqTranscriptionService>();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment()|| app.Environment.IsProduction() )
{
    // 2. Maps the /openapi/v1.json endpoint
    app.MapOpenApi(); 
    
    // 3. Maps the Scalar UI (which reads the endpoint above)
      app.MapScalarApiReference(options => 
        {
            options.WithTitle("finalyearproject")
                .WithTheme(ScalarTheme.Moon) // Try 'Purple', 'Solarized', or 'DeepSpace'
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });
    }
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();