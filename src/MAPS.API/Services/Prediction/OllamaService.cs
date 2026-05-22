using System.Text;
using System.Text.Json;
using MAPS.ML.Prediction;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.Prediction;

public interface IOllamaService
{
    Task<TextPredictionResult> GeneratePredictionAsync(DiseaseType disease, string prompt);
    Task<string>               GenerateResponseAsync(string systemPrompt, string userPrompt);
    Task<float[]>              GenerateEmbeddingAsync(string text);
    Task<bool>                 IsAvailableAsync();
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient              _http;
    private readonly string                 _model;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(IConfiguration config, ILogger<OllamaService> logger)
    {
        _logger = logger;
        _model  = config["Ollama:Model"] ?? "llama3";
        _http   = new HttpClient
        {
            BaseAddress = new Uri(config["Ollama:BaseUrl"] ?? "http://ollama:11434"),
            Timeout     = TimeSpan.FromSeconds(120)
        };
    }

    // ── Disease Prediction via LLM ────────────────────────────────────────────
    public async Task<TextPredictionResult> GeneratePredictionAsync(
        DiseaseType disease, string prompt)
    {
        var systemPrompt = $"""
            You are a clinical decision support AI assistant for MAPS Medical Platform.
            You are analysing a patient case for {disease}.

            Respond ONLY in valid JSON format with this exact schema:
            {{
              "primaryDiagnosis": "string",
              "confidence": 0.00,
              "reasoningChain": "string",
              "differentials": [
                {{ "rank": 1, "condition": "string", "probability": 0.00,
                   "reasoning": "string", "tests": ["string"] }}
              ],
              "confirmatoryTests": ["string"]
            }}

            IMPORTANT: confidence must be between 0.0 and 1.0.
            Include 3-5 differential diagnoses ranked by probability.
            Always add the disclaimer internally — do not include it in the JSON.
            """;

        try
        {
            var rawResponse = await GenerateResponseAsync(systemPrompt, prompt);
            var result      = ParsePredictionResponse(rawResponse);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama prediction failed for {Disease}", disease);
            return new TextPredictionResult
            {
                PrimaryDiagnosis = $"LLM prediction unavailable for {disease}",
                Confidence       = 0.0f,
                ReasoningChain   = "Ollama service error — falling back to structured model"
            };
        }
    }

    // ── Generic Response Generation ───────────────────────────────────────────
    public async Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt)
    {
        var requestBody = new
        {
            model  = _model,
            stream = false,
            options = new { temperature = 0.3, num_predict = 2048 },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            }
        };

        var json     = JsonSerializer.Serialize(requestBody);
        var content  = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var parsed       = JsonSerializer.Deserialize<JsonElement>(responseJson);

        return parsed
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    // ── Embedding Generation for RAG ──────────────────────────────────────────
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var requestBody = new { model = _model, prompt = text };
        var json        = JsonSerializer.Serialize(requestBody);
        var content     = new StringContent(json, Encoding.UTF8, "application/json");
        var response    = await _http.PostAsync("/api/embeddings", content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Embedding generation failed — returning empty vector");
            return new float[1536];
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var parsed       = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var embedding    = parsed.GetProperty("embedding")
            .EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();

        return embedding;
    }

    // ── Health Check ──────────────────────────────────────────────────────────
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Parse LLM JSON Response ───────────────────────────────────────────────
    private static TextPredictionResult ParsePredictionResponse(string rawResponse)
    {
        // Strip markdown code fences if present
        var cleaned = rawResponse
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        // Find the JSON object
        var startIdx = cleaned.IndexOf('{');
        var endIdx   = cleaned.LastIndexOf('}');
        if (startIdx < 0 || endIdx < 0)
            throw new InvalidOperationException("No valid JSON found in LLM response");

        var jsonStr = cleaned[startIdx..(endIdx + 1)];
        var doc     = JsonSerializer.Deserialize<JsonElement>(jsonStr);

        var result = new TextPredictionResult
        {
            PrimaryDiagnosis = doc.GetProperty("primaryDiagnosis").GetString() ?? "",
            Confidence       = doc.GetProperty("confidence").GetSingle(),
            ReasoningChain   = doc.GetProperty("reasoningChain").GetString() ?? ""
        };

        if (doc.TryGetProperty("differentials", out var diffs))
        {
            foreach (var d in diffs.EnumerateArray())
            {
                result.Differentials.Add(new DifferentialEntry
                {
                    Rank        = d.GetProperty("rank").GetInt32(),
                    Condition   = d.GetProperty("condition").GetString() ?? "",
                    Probability = d.GetProperty("probability").GetSingle(),
                    Reasoning   = d.GetProperty("reasoning").GetString() ?? "",
                    Tests       = d.TryGetProperty("tests", out var tests)
                        ? tests.EnumerateArray().Select(t => t.GetString() ?? "").ToList()
                        : new List<string>()
                });
            }
        }

        if (doc.TryGetProperty("confirmatoryTests", out var ctests))
        {
            result.ConfirmatoryTests = ctests.EnumerateArray()
                .Select(t => t.GetString() ?? "").ToList();
        }

        return result;
    }
}
