using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Vosk;
using AiModelDemo.Services;

namespace AiModelDemo.Hubs;

public class SpeechRecognitionHub : Hub
{
    private const int BufferSize = 4096;
    private readonly ISpeechRecognitionService _speechService;
    private readonly ILogger<SpeechRecognitionHub> _logger;
    
    // Store recognizers per connection
    private static readonly Dictionary<string, VoskRecognizer> _recognizers = new();

    public SpeechRecognitionHub(
        ISpeechRecognitionService speechService,
        ILogger<SpeechRecognitionHub> logger)
    {
        _speechService = speechService;
        _logger = logger;
    }

    /// <summary>
    /// Initialize speech recognition for a specific language
    /// </summary>
    public async Task InitializeRecognition(string language)
    {
        try
        {
            var recognizer = _speechService.CreateRecognizer(language);
            _recognizers[Context.ConnectionId] = recognizer;
            
            _logger.LogInformation("Recognition initialized for connection {ConnectionId} with language {Language}", 
                Context.ConnectionId, language);
            
            await Clients.Caller.SendAsync("RecognitionInitialized", new { success = true, language });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize recognition for language {Language}", language);
            await Clients.Caller.SendAsync("Error", new { message = ex.Message });
        }
    }

    /// <summary>
    /// Process audio data sent from client
    /// </summary>
    public async Task ProcessAudio(byte[] audioData)
    {
        _logger.LogInformation("ProcessAudio received {ByteCount} bytes", audioData?.Length ?? 0);
        
        if (audioData == null || audioData.Length == 0)
        {
            _logger.LogWarning("Received empty audio data");
            return;
        }

        if (!_recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Recognition not initialized. Call InitializeRecognition first." });
            return;
        }

        try
        {
            if (recognizer.AcceptWaveform(audioData, audioData.Length))
            {
                // Final result
                var jsonResult = recognizer.Result();
                _logger.LogInformation("Final result: {Result}", jsonResult);
                var result = JsonSerializer.Deserialize<VoskResult>(jsonResult);
                
                if (!string.IsNullOrWhiteSpace(result?.Text))
                {
                    _logger.LogInformation("Sending FinalTranscription to client: {Text}", result.Text);
                    await Clients.Caller.SendAsync("FinalTranscription", new
                    {
                        text = result.Text,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
            }
            else
            {
                // Partial result
                var partialResult = recognizer.PartialResult();
                _logger.LogInformation("Partial result raw: {Result}", partialResult);
                var result = JsonSerializer.Deserialize<VoskPartialResult>(partialResult);
                _logger.LogInformation("Partial result deserialized: Partial='{Partial}'", result?.Partial ?? "NULL");
                
                if (!string.IsNullOrWhiteSpace(result?.Partial))
                {
                    _logger.LogInformation("Sending PartialTranscription to client: {Text}", result.Partial);
                    await Clients.Caller.SendAsync("PartialTranscription", new
                    {
                        text = result.Partial,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                    _logger.LogInformation("PartialTranscription sent successfully");
                }
                else
                {
                    _logger.LogInformation("Partial result is empty, not sending to client");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
            await Clients.Caller.SendAsync("Error", new { message = "Error processing audio" });
        }
    }

    /// <summary>
    /// End current utterance and get final result
    /// </summary>
    public async Task EndUtterance()
    {
        if (!_recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
        {
            return;
        }

        try
        {
            var finalResult = recognizer.FinalResult();
            var result = JsonSerializer.Deserialize<VoskResult>(finalResult);
            
            if (!string.IsNullOrWhiteSpace(result?.Text))
            {
                await Clients.Caller.SendAsync("FinalTranscription", new
                {
                    text = result.Text,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            
            recognizer.Reset();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending utterance");
        }
    }

    /// <summary>
    /// Clean up when client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
        {
            recognizer.Dispose();
            _recognizers.Remove(Context.ConnectionId);
            _logger.LogInformation("Recognizer disposed for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Helper classes for JSON deserialization
    private class VoskResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class VoskPartialResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("partial")]
        public string Partial { get; set; } = string.Empty;
    }
}
