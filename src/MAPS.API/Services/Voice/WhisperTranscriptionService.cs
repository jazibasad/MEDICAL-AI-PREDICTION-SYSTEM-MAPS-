using System.Net.Http.Headers;
using System.Text.Json;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Services.Voice;

public class TranscriptionResult
{
    public string Text     { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool   Corrected{ get; set; }
}

public interface IWhisperTranscriptionService
{
    Task<ApiResponse<TranscriptionResult>> TranscribeAsync(
        Stream audioStream, string fileName, string contentType);
    Task<bool> IsAvailableAsync();
}

public class WhisperTranscriptionService : IWhisperTranscriptionService
{
    private readonly HttpClient                          _http;
    private readonly ILogger<WhisperTranscriptionService> _logger;

    // Medical term correction dictionary
    // Maps common speech recognition errors to correct clinical terms
    private static readonly Dictionary<string, string> MedicalCorrections =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Diseases
        { "die a beat ease",       "diabetes"          },
        { "die a beat is",         "diabetes"          },
        { "new monia",             "pneumonia"         },
        { "new moan ia",           "pneumonia"         },
        { "high per tension",      "hypertension"      },
        { "high pertension",       "hypertension"      },
        { "arte rio sclerosis",    "arteriosclerosis"  },
        { "my oh card ial",        "myocardial"        },
        { "my o card ial",         "myocardial"        },
        { "tack y card ia",        "tachycardia"       },
        { "brady card ia",         "bradycardia"       },
        { "ath er o sclerosis",    "atherosclerosis"   },

        // Medications
        { "met for min",           "metformin"         },
        { "lisa no pril",          "lisinopril"        },
        { "at or va stat in",      "atorvastatin"      },
        { "am ox i cil in",        "amoxicillin"       },
        { "om e pra zole",         "omeprazole"        },
        { "sal bu ta mol",         "salbutamol"        },
        { "pred nis o lone",       "prednisolone"      },

        // Investigations
        { "e c g",                 "ECG"               },
        { "e k g",                 "ECG"               },
        { "c b c",                 "CBC"               },
        { "m r i",                 "MRI"               },
        { "c t scan",              "CT scan"           },
        { "h b a one c",           "HbA1c"             },
        { "hba one c",             "HbA1c"             },
        { "s p o two",             "SpO2"              },

        // Units
        { "milli grams",           "mg"                },
        { "micro grams",           "mcg"               },
        { "mill i meters",         "mm"                },
        { "millimeters of mercury","mmHg"              },

        // Clinical terms
        { "dif er en tial",        "differential"      },
        { "pre scrip tion",        "prescription"      },
        { "con tra in di cated",   "contraindicated"   },
        { "pro phy lac tic",       "prophylactic"      },
        { "as ymp to matic",       "asymptomatic"      },
    };

    public WhisperTranscriptionService(
        IConfiguration                         config,
        ILogger<WhisperTranscriptionService>   logger)
    {
        _logger = logger;
        _http   = new HttpClient
        {
            BaseAddress = new Uri(config["Whisper:BaseUrl"] ?? "http://maps-whisper:8090"),
            Timeout     = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<ApiResponse<TranscriptionResult>> TranscribeAsync(
        Stream audioStream, string fileName, string contentType)
    {
        try
        {
            using var form    = new MultipartFormDataContent();
            using var content = new StreamContent(audioStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "audio", fileName);

            var response = await _http.PostAsync("/transcribe", form);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Whisper transcription failed: {Error}", error);
                return ApiResponse<TranscriptionResult>.Fail(
                    "Voice transcription failed. Please try again.");
            }

            var json   = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);
            var raw    = parsed.GetProperty("text").GetString() ?? string.Empty;

            // Apply medical term correction
            var (corrected, wasCorrected) = ApplyMedicalCorrections(raw);

            _logger.LogInformation(
                "Transcription complete: {Chars} chars, corrected={Corrected}",
                corrected.Length, wasCorrected);

            return ApiResponse<TranscriptionResult>.Ok(new TranscriptionResult
            {
                Text      = corrected,
                Language  = parsed.TryGetProperty("language", out var lang)
                            ? lang.GetString() ?? "en" : "en",
                Corrected = wasCorrected
            });
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Whisper transcription timed out");
            return ApiResponse<TranscriptionResult>.Fail(
                "Transcription timed out. Please try a shorter audio clip.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice transcription error");
            return ApiResponse<TranscriptionResult>.Fail(
                "Voice transcription service unavailable.");
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static (string text, bool corrected) ApplyMedicalCorrections(string raw)
    {
        var result    = raw;
        var corrected = false;

        foreach (var (wrong, correct) in MedicalCorrections)
        {
            if (result.Contains(wrong, StringComparison.OrdinalIgnoreCase))
            {
                result    = result.Replace(wrong, correct,
                              StringComparison.OrdinalIgnoreCase);
                corrected = true;
            }
        }

        return (result, corrected);
    }
}
