using AiModelDemo.Core.Interfaces;
using AiModelDemo.Infrastructure.Speech;
using AiModelDemo.Infrastructure.LLM;
using AiModelDemo.Hubs;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR with MessagePack protocol for efficient binary data transfer
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB for audio chunks
}).AddMessagePackProtocol();

// Register services as singletons
builder.Services.AddSingleton<ISpeechRecognitionService, VoskSpeechRecognitionService>();

// Register IChatClient for Microsoft.Extensions.AI
builder.Services.AddChatClient(
    new OllamaApiClient(new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434"), builder.Configuration["Ollama:Model"] ?? "meditron:7b"));

builder.Services.AddSingleton<ILLMService, OllamaService>();

// Configure CORS for React Vite dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowReactApp");

app.UseRouting();

// Map controllers and SignalR hubs
app.MapControllers();
app.MapHub<SpeechRecognitionHub>("/hubs/speech");

app.Run();