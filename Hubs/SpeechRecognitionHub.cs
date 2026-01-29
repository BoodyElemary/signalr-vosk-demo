using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Vosk;
using AiModelDemo.Core.Interfaces;

namespace AiModelDemo.Hubs;

public class SpeechRecognitionHub : Hub
{
    private readonly ISpeechRecognitionService _speechService;
    private readonly ILogger<SpeechRecognitionHub> _logger;
    
    // Store recognizers per connection
    private static readonly Dictionary<string, VoskRecognizer> Recognizers = new();

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
            Recognizers[Context.ConnectionId] = recognizer;
            
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
        if (audioData.Length == 0)
        {
            _logger.LogWarning("Received empty audio data");
            return;
        }

        if (!Recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
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
                var result = JsonSerializer.Deserialize<VoskResult>(jsonResult);
                
                if (!string.IsNullOrWhiteSpace(result?.Text))
                {
                    _logger.LogDebug("Final transcription: {Text}", result.Text);
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
                var result = JsonSerializer.Deserialize<VoskPartialResult>(partialResult);
                
                if (!string.IsNullOrWhiteSpace(result?.Partial))
                {
                    await Clients.Caller.SendAsync("PartialTranscription", new
                    {
                        text = result.Partial,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
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
        if (!Recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
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
        if (Recognizers.TryGetValue(Context.ConnectionId, out var recognizer))
        {
            recognizer.Dispose();
            Recognizers.Remove(Context.ConnectionId);
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
