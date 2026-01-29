using AiModelDemo.Services;
using AiModelDemo.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable WebSockets (required for WebSocket endpoint in SpeechController)
app.UseWebSockets();

app.UseCors("AllowReactApp");

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<SpeechRecognitionHub>("/hubs/speech");

app.Run();