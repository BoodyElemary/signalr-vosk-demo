using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiModelDemo.Core.Interfaces;
using AiModelDemo.Core.Models;
using Microsoft.Extensions.AI;

namespace AiModelDemo.Infrastructure.LLM;

public class OllamaService : ILLMService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<OllamaService> _logger;

    // Common prompt-injection / jailbreak phrases — fast check, no extra LLM call needed
    private static readonly Regex _maliciousPattern = new(
        @"ignore\s+(previous|all|above|prior)\s+instructions?|" +
        @"forget\s+(your|all|previous|prior)\s+(instructions?|rules?|context)|" +
        @"you\s+are\s+now\s+(a\s+)?|" +
        @"disregard\s+(all\s+)?(previous|prior)\s+instructions?|" +
        @"new\s+instructions?:|" +
        @"system\s+prompt|" +
        @"override\s+(your\s+)?(instructions?|rules?)|" +
        @"act\s+as\s+(if\s+you\s+are|a\s+)?|" +
        @"jailbreak|" +
        @"dan\s+mode",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OllamaService(IChatClient chatClient, ILogger<OllamaService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    // ─── Public: Structure Transcript ────────────────────────────────────────

    public async Task<StructuredTranscript> StructureTranscriptAsync(
        string transcription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Structuring transcript using IChatClient");

            var messages = BuildTranscriptMessages(transcription);

            var response = await _chatClient.GetResponseAsync<StructuredTranscript>(
                messages,
                options: new ChatOptions { Temperature = 0f },
                cancellationToken: cancellationToken);

            if (response?.Result != null)
            {
                _logger.LogInformation("Transcript structured successfully with {count} lines", response.Result.Lines.Count);
                return response.Result;
            }

            throw new InvalidOperationException("Empty or invalid structured transcript response");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Transcript structuring was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to structure transcript");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamStructureTranscriptAsync(
        string transcription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming transcript structuring");

        var messages = BuildTranscriptMessages(transcription);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
            messages,
            options: new ChatOptions { Temperature = 0f },
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    // ─── Public: Generate SOAP Note ──────────────────────────────────────────

    public async Task<SOAPNote> GenerateSOAPNoteAsync(
        string transcription,
        string? patientContext = null,
        string? consultationType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating SOAP note using IChatClient");

            var messages = BuildSOAPMessages(transcription, patientContext, consultationType);

            var response = await _chatClient.GetResponseAsync<SOAPNote>(
                messages,
                options: new ChatOptions { Temperature = 0f },
                cancellationToken: cancellationToken);

            if (response?.Result != null)
            {
                _logger.LogInformation("SOAP note generated and structured successfully");
                return response.Result;
            }

            throw new InvalidOperationException("Empty or invalid structured response from IChatClient");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SOAP note generation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SOAP note");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamGenerateSOAPNoteAsync(
        string transcription,
        string? patientContext = null,
        string? consultationType = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming SOAP note generation");

        var messages = BuildSOAPMessages(transcription, patientContext, consultationType);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
            messages,
            options: new ChatOptions { Temperature = 0f },
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public async IAsyncEnumerable<string> StreamGenerateSOAPNoteFromRawAsync(
        string rawTranscription,
        string? patientContext = null,
        string? consultationType = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming SOAP note generation from raw STT (single prompt)");

        var messages = BuildRawSTTMessages(rawTranscription, patientContext, consultationType);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
            messages,
            options: new ChatOptions { Temperature = 0f },
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    // ─── Private: Fast Malicious Input Check ─────────────────────────────────

    private static bool IsMaliciousInput(string input) =>
        _maliciousPattern.IsMatch(input);

    // ─── Private: Message Builders ────────────────────────────────────────────

    private static List<ChatMessage> BuildRawSTTMessages(
        string rawTranscription, string? patientContext, string? consultationType) =>
        new()
        {
            new ChatMessage(ChatRole.System, BuildRawSTTSystemPrompt()),
            new ChatMessage(ChatRole.User, BuildRawSTTPrompt(rawTranscription, patientContext, consultationType))
        };

    private static List<ChatMessage> BuildTranscriptMessages(string transcription) =>
        new()
        {
            new ChatMessage(ChatRole.System, BuildTranscriptSystemPrompt()),
            new ChatMessage(ChatRole.User, transcription)
        };

    private static List<ChatMessage> BuildSOAPMessages(
        string transcription, string? patientContext, string? consultationType) =>
        new()
        {
            new ChatMessage(ChatRole.System, BuildSystemPrompt()),
            new ChatMessage(ChatRole.User, BuildPrompt(transcription, patientContext, consultationType))
        };

    // ─── Private: Prompt Builders ─────────────────────────────────────────────

    private static string BuildTranscriptSystemPrompt()
    {
        return """
            You are a medical transcription specialist.
            You will receive a raw audio transcription from a doctor-patient encounter.
            The transcription has no speaker labels — it is a continuous block of text.

            Your job is to read the transcription carefully and identify who said what,
            then return a structured JSON array of conversation lines.

            You MUST respond ONLY with a valid JSON object using this exact structure:
            {
              "lines": [
                { "speaker": "Doctor", "text": "..." },
                { "speaker": "Patient", "text": "..." }
              ]
            }

            === RULES ===
            - Speaker must be either "Doctor" or "Patient" only
            - Split the conversation into natural turns — one entry per spoken turn
            - Do not merge multiple turns into one entry
            - Do not split a single turn into multiple entries
            - Do not add, remove, or paraphrase any words from the original transcription
            - Keep the exact wording as spoken
            - Respond with JSON only — no explanation, no markdown, no extra text
            """;
    }

    private static string BuildRawSTTSystemPrompt()
    {
        return """
            You are an expert clinical documentation specialist.
            You will receive a raw audio transcription from a doctor-patient encounter.
            The transcription has NO speaker labels — it is unstructured, continuous text
            produced by a speech-to-text engine and may contain minor errors.

            Your job is to do TWO things in a single response:

            STEP 1 — Structure the conversation:
            Read the transcription carefully and determine which parts were spoken
            by the Doctor and which by the Patient. Split it into natural speaking turns.

            STEP 2 — Generate the SOAP note:
            Using your understanding of who said what, extract all clinical information
            and produce a complete SOAP note.

            You MUST respond ONLY with a valid JSON object using this exact structure:
            {
              "structuredTranscript": {
                "lines": [
                  { "speaker": "Doctor", "text": "..." },
                  { "speaker": "Patient", "text": "..." }
                ]
              },
              "soapNote": {
                "subjective": "...",
                "objective": "...",
                "assessment": "...",
                "plan": "..."
              }
            }

            === STRUCTURED TRANSCRIPT RULES ===
            - Speaker must be either "Doctor" or "Patient" only
            - Split the conversation into natural turns — one entry per spoken turn
            - Do not merge multiple turns into one entry
            - Do not add, remove, or paraphrase any words from the original transcription
            - Keep the exact wording as spoken

            === SOAP NOTE — WHAT TO EXTRACT FOR EACH FIELD ===

            SUBJECTIVE — what the PATIENT says and reports:
            - Chief complaint ("I have a cough", "I feel chest pain")
            - Symptom duration, onset, character, severity
            - Associated symptoms, medical history, medications, allergies the patient mentions

            OBJECTIVE — what the DOCTOR does, measures, or observes:
            - Vital signs (temperature, heart rate, blood pressure, oxygen saturation)
            - Physical examination findings (lung sounds, heart sounds, palpation findings)
            - Diagnostic actions performed (ECG, auscultation, blood pressure check)

            ASSESSMENT — what the DOCTOR concludes:
            - Write as a clinical impression (e.g. "Mild upper respiratory tract infection")
            - Severity, differential diagnoses mentioned

            PLAN — what the DOCTOR recommends or orders:
            - Tests ordered, medications prescribed, lifestyle advice, follow-up, referrals

            === RULES ===
            - Fill ALL four SOAP fields: subjective, objective, assessment, and plan
            - Use professional medical terminology throughout
            - Only document information explicitly stated or clearly implied in the transcript
            - Never fabricate or assume information not present in the transcript
            - Write in third-person clinical style (e.g. "Patient reports...", "Physician noted...")
            - If a section truly has no data, write "Not documented in transcript"
            - Do not copy instructions into the output
            - Respond with JSON only — no explanation, no markdown, no extra text
            """;
    }

    private static string BuildRawSTTPrompt(
        string rawTranscription, string? patientContext, string? consultationType)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(consultationType) || !string.IsNullOrWhiteSpace(patientContext))
        {
            sb.AppendLine("=== CLINICAL CONTEXT ===");

            if (!string.IsNullOrWhiteSpace(consultationType))
                sb.AppendLine($"Consultation Type : {consultationType}");

            if (!string.IsNullOrWhiteSpace(patientContext))
                sb.AppendLine($"Patient Context   : {patientContext}");

            sb.AppendLine();
        }

        sb.AppendLine("=== RAW SPEECH-TO-TEXT TRANSCRIPTION (no speaker labels) ===");
        sb.AppendLine(rawTranscription);
        sb.AppendLine();

        sb.AppendLine("=== TASK ===");
        sb.AppendLine("Read the raw transcription above.");
        sb.AppendLine("1. Identify each speaking turn and label it as Doctor or Patient.");
        sb.AppendLine("2. Extract all clinical information and fill ALL four SOAP fields.");
        sb.AppendLine("Do not copy instructions. Do not add information outside of the transcript.");
        sb.AppendLine("Respond with the JSON object only — both structuredTranscript and soapNote.");

        return sb.ToString();
    }

    private static string BuildSystemPrompt()
    {
        return """
            You are an expert clinical documentation specialist with deep knowledge of 
            medical terminology, clinical reasoning, and SOAP note formatting standards.

            Your role is to analyze a doctor-patient conversation transcript and extract 
            all clinical information into a SOAP note.

            You MUST respond ONLY with a valid JSON object using this exact structure:
            {
              "subjective": "...",
              "objective": "...",
              "assessment": "...",
              "plan": "..."
            }

            === WHAT TO EXTRACT FOR EACH FIELD ===

            SUBJECTIVE — what the PATIENT says and reports:
            - Chief complaint ("I have a cough", "I feel chest pain")
            - Symptom duration, onset, character, severity
            - Associated symptoms the patient mentions
            - Medical history, medications, allergies the patient mentions

            OBJECTIVE — what the DOCTOR does, measures, or observes:
            - Vital signs the doctor reads out (temperature, heart rate, blood pressure, oxygen saturation)
            - Physical examination findings the doctor reports (lung sounds, heart sounds, palpation findings)
            - Diagnostic actions the doctor performs (ECG, auscultation, blood pressure check)

            ASSESSMENT — what the DOCTOR concludes:
            - Write as a clinical impression, not a sentence (e.g. "Mild upper respiratory tract infection" not "The diagnosis is...")
            - The diagnosis or clinical impression the doctor states
            - Severity of the condition
            - Any differential diagnoses mentioned

            PLAN — what the DOCTOR recommends or orders:
            - Tests ordered
            - Medications prescribed or recommended
            - Lifestyle advice given (rest, fluids, diet)
            - Follow-up instructions
            - Referrals made
            - Warning signs to watch for

            === RULES ===
            - You MUST fill ALL four fields: subjective, objective, assessment, and plan
            - Use professional medical terminology throughout
            - Only document information explicitly stated or clearly clinically implied in the transcript
            - Never fabricate, hallucinate, or assume information not present in the transcript
            - Be concise yet clinically complete — avoid vague or generic statements
            - Write in third-person clinical documentation style (e.g. "Patient reports...", "Physician noted...")
            - If a section truly has no data from the transcript, write "Not documented in transcript"
            - Do not copy any instructions or rules into the output
            - Do not use your prior medical knowledge to fill in gaps
            - Respond with JSON only — no explanation, no markdown, no extra text
            """;
    }

    private static string BuildPrompt(string transcription, string? patientContext, string? consultationType)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(consultationType) || !string.IsNullOrWhiteSpace(patientContext))
        {
            sb.AppendLine("=== CLINICAL CONTEXT ===");

            if (!string.IsNullOrWhiteSpace(consultationType))
                sb.AppendLine($"Consultation Type : {consultationType}");

            if (!string.IsNullOrWhiteSpace(patientContext))
                sb.AppendLine($"Patient Context   : {patientContext}");

            sb.AppendLine();
        }

        sb.AppendLine("=== DOCTOR-PATIENT TRANSCRIPT ===");
        sb.AppendLine(transcription);
        sb.AppendLine();

        sb.AppendLine("=== TASK ===");
        sb.AppendLine("Read the transcript above carefully.");
        sb.AppendLine("Extract the actual clinical information spoken by the doctor and patient.");
        sb.AppendLine("Fill ALL four SOAP fields: subjective, objective, assessment, and plan.");
        sb.AppendLine("Do not copy instructions. Do not add information outside of the transcript.");
        sb.AppendLine("Write only what was said or clearly implied in the transcript.");
        sb.AppendLine("Respond with the JSON object only.");

        return sb.ToString();
    }
}