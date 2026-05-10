using System.Text;
using System.Text.Json;
using AiModelDemo.Core.Interfaces;
using AiModelDemo.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiModelDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SOAPNotesController : ControllerBase
{
    private readonly ILLMService _llmService;
    private readonly ILogger<SOAPNotesController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    public SOAPNotesController(ILLMService llmService, ILogger<SOAPNotesController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a structured SOAP note from transcription text
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(SOAPNote), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SOAPNote>> GenerateSOAPNote(
        [FromBody] GenerateSOAPNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcription))
            return BadRequest(new { message = "Transcription is required" });

        try
        {
            _logger.LogInformation("Generating SOAP note from transcription");

            var soapNote = await _llmService.GenerateSOAPNoteAsync(
                request.Transcription,
                request.PatientContext,
                request.ConsultationType,
                cancellationToken);

            return Ok(soapNote);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("malicious"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SOAP note");
            return StatusCode(500, new { message = "Failed to generate SOAP note", error = ex.Message });
        }
    }

    /// <summary>
    /// Stream SOAP note generation token-by-token via Server-Sent Events.
    /// Each SSE event is a raw text token; the final event has id "done" and contains the full JSON.
    /// </summary>
    [HttpPost("generate/stream")]
    public async Task StreamGenerateSOAPNote(
        [FromBody] GenerateSOAPNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcription))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = "Transcription is required" }, cancellationToken);
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var fullText = new StringBuilder();

            await foreach (var token in _llmService.StreamGenerateSOAPNoteAsync(
                request.Transcription,
                request.PatientContext,
                request.ConsultationType,
                cancellationToken))
            {
                fullText.Append(token);
                var escaped = token.Replace("\n", "\\n");
                await Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Send the full assembled JSON as the final "done" event
            await Response.WriteAsync($"id: done\ndata: {fullText}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("malicious"))
        {
            await Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing to do
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming SOAP note");
            await Response.WriteAsync($"event: error\ndata: Failed to generate SOAP note\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Check if the Ollama LLM service is available
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckHealth(CancellationToken cancellationToken)
    {
        var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);
        return Ok(isAvailable);
    }

    /// <summary>
    /// Structure a raw transcription into labeled doctor-patient conversation lines
    /// </summary>
    [HttpPost("structure-transcript")]
    [ProducesResponseType(typeof(StructuredTranscript), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StructuredTranscript>> StructureTranscript(
        [FromBody] StructureTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcription))
            return BadRequest(new { message = "Transcription is required" });

        try
        {
            var result = await _llmService.StructureTranscriptAsync(
                request.Transcription,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error structuring transcript");
            return StatusCode(500, new { message = "Failed to structure transcript", error = ex.Message });
        }
    }

    /// <summary>
    /// Stream transcript structuring token-by-token via Server-Sent Events.
    /// Each SSE event is a raw text token; the final event has id "done" and contains the full JSON.
    /// </summary>
    [HttpPost("structure-transcript/stream")]
    public async Task StreamStructureTranscript(
        [FromBody] StructureTranscriptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcription))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = "Transcription is required" }, cancellationToken);
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var fullText = new StringBuilder();

            await foreach (var token in _llmService.StreamStructureTranscriptAsync(
                request.Transcription,
                cancellationToken))
            {
                fullText.Append(token);
                var escaped = token.Replace("\n", "\\n");
                await Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Send the full assembled JSON as the final "done" event
            await Response.WriteAsync($"id: done\ndata: {fullText}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing to do
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming transcript structuring");
            await Response.WriteAsync($"event: error\ndata: Failed to structure transcript\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Combined pipeline: takes raw/fuzzy STT text, streams phase 1 (transcript structuring)
    /// then automatically streams phase 2 (SOAP note generation) — all in a single SSE connection.
    ///
    /// SSE event protocol:
    ///   event: phase          data: structuring          — phase 1 starting
    ///   data: &lt;token&gt;                                    — phase 1 streaming tokens
    ///   id: structure-done    data: &lt;full JSON&gt;          — StructuredTranscript complete
    ///   event: phase          data: generating           — phase 2 starting
    ///   data: &lt;token&gt;                                    — phase 2 streaming tokens
    ///   id: done              data: &lt;full JSON&gt;          — SOAPNote complete
    /// </summary>
    [HttpPost("generate-from-raw/stream")]
    public async Task StreamGenerateFromRaw(
        [FromBody] GenerateFromRawRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawTranscription))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = "RawTranscription is required" }, cancellationToken);
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var fullText = new StringBuilder();

            await foreach (var token in _llmService.StreamGenerateSOAPNoteFromRawAsync(
                request.RawTranscription,
                request.PatientContext,
                request.ConsultationType,
                cancellationToken))
            {
                fullText.Append(token);
                var escaped = token.Replace("\n", "\\n");
                await Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Final event contains the complete assembled SOAP JSON
            await Response.WriteAsync($"id: done\ndata: {fullText}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("malicious"))
        {
            await Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing to do
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in generate-from-raw stream");
            await Response.WriteAsync($"event: error\ndata: Failed to generate SOAP note\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
