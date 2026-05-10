using System.Text;
using AiModelDemo.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace AiModelDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILLMService _llmService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILLMService llmService, IChatClient chatClient, ILogger<HealthController> logger)
    {
        _llmService = llmService;
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint that sends a direct message to the ML model
    /// and returns the response as a stream via Server-Sent Events.
    /// </summary>
    [HttpPost("check")]
    public async Task CheckModelHealth(
        [FromQuery] string? message = null,
        CancellationToken cancellationToken = default)
    {
        var healthCheckMessage = message ?? "hi";

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            _logger.LogInformation("Health check started with message: {message}", healthCheckMessage);

            var fullResponse = new StringBuilder();
            var messages = new List<ChatMessage> { new(ChatRole.User, healthCheckMessage) };

            // Stream the response directly from the model
            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                messages,
                options: new ChatOptions { Temperature = 0.7f },
                cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    fullResponse.Append(update.Text);
                    var escaped = update.Text.Replace("\n", "\\n").Replace("\"", "\\\"");
                    await Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }

            // Send completion event with full response
            _logger.LogInformation("Health check completed successfully");
            await Response.WriteAsync($"id: done\ndata: {fullResponse}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            Response.StatusCode = 503;
            await Response.WriteAsync($"id: error\ndata: Model health check failed: {ex.Message}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Simple health check that returns JSON status (non-streaming)
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealthStatus(CancellationToken cancellationToken)
    {
        try
        {
            var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                return Ok(new HealthStatus
                {
                    Status = "healthy",
                    Message = "ML model is operational"
                });
            }

            return StatusCode(503, new HealthStatus
            {
                Status = "unhealthy",
                Message = "ML model is not responding"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health status check failed");
            return StatusCode(503, new HealthStatus
            {
                Status = "unhealthy",
                Message = $"Health check failed: {ex.Message}"
            });
        }
    }
}

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
