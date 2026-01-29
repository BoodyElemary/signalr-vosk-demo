using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiModelDemo.Services;
using Vosk;
namespace AiModelDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechController : ControllerBase
{
    private const int BufferSize = 4096;
    private readonly ISpeechRecognitionService _speechService;
    private readonly ILogger<SpeechController> _logger;

    public SpeechController(
        ISpeechRecognitionService speechService,
        ILogger<SpeechController> logger)
    {
        _speechService = speechService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of available language models
    /// </summary>
    [HttpGet("languages")]
    public ActionResult<IEnumerable<string>> GetAvailableLanguages()
    {
        var languages = _speechService.GetAvailableLanguages();
        return Ok(languages);
    }

    /// <summary>
    /// WebSocket endpoint for real-time speech recognition
    /// </summary>
    [HttpGet("ws")]
    public async Task<IActionResult> WebSocket([FromQuery] string lang = "en-us")
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return BadRequest("WebSocket connection required");
        }

        try
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocketConnection(webSocket, lang);
            return Ok();
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Model not found for language: {Language}", lang);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket connection");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task HandleWebSocketConnection(WebSocket webSocket, string language)
    {
        VoskRecognizer? recognizer = null;
        
        try
        {
            recognizer = _speechService.CreateRecognizer(language);
            _logger.LogInformation("WebSocket connected for language: {Language}", language);

            var buffer = new byte[BufferSize];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by client",
                        CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Process audio data
                    if (recognizer.AcceptWaveform(buffer, result.Count))
                    {
                        // Final result
                        var jsonResult = recognizer.Result();
                        await SendTranscriptionAsync(webSocket, jsonResult, isFinal: true);
                    }
                    else
                    {
                        // Partial result
                        var partialResult = recognizer.PartialResult();
                        await SendTranscriptionAsync(webSocket, partialResult, isFinal: false);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle control messages
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleControlMessage(webSocket, message, recognizer);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket messages");
            await SendErrorAsync(webSocket, ex.Message);
        }
        finally
        {
            recognizer?.Dispose();
            _logger.LogInformation("WebSocket connection closed");
        }
    }

    private async Task SendTranscriptionAsync(WebSocket webSocket, string jsonResult, bool isFinal)
    {
        var response = new
        {
            type = isFinal ? "final" : "partial",
            result = jsonResult,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);

        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
    }

    private async Task HandleControlMessage(WebSocket webSocket, string message, VoskRecognizer recognizer)
    {
        try
        {
            var controlMsg = JsonSerializer.Deserialize<ControlMessage>(message);

            if (controlMsg?.Command == "end_utterance")
            {
                var finalResult = recognizer.FinalResult();
                await SendTranscriptionAsync(webSocket, finalResult, isFinal: true);
                recognizer.Reset();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid control message received");
        }
    }

    private async Task SendErrorAsync(WebSocket webSocket, string error)
    {
        var response = new
        {
            type = "error",
            message = error
        };

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);

        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
    }

    private class ControlMessage
    {
        public string? Command { get; set; }
    }
}
