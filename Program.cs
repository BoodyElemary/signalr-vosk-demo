using AiModelDemo.Core.Interfaces;
using AiModelDemo.Infrastructure.Speech;
using AiModelDemo.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR with MessagePack protocol for efficient binary data transfer
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB for audio chunks
}).AddMessagePackProtocol();

// Register speech recognition service as singleton
builder.Services.AddSingleton<ISpeechRecognitionService, VoskSpeechRecognitionService>();

// Configure CORS for React Vite dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5174", "http://localhost:3000") // Vite default ports
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

app.UseCors("AllowReactApp");


// Map SignalR hub
app.MapHub<SpeechRecognitionHub>("/hubs/speech");

app.Run();