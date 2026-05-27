using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Services.Prediction;
using MAPS.Shared.DTOs.Chatbot;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Services.Literature;

public interface ILiteratureSearchService
{
    Task<ApiResponse<List<LiteratureSearchResult>>> SearchAsync(
        LiteratureSearchRequest req, Guid doctorId);
    Task IndexDocumentAsync(string title, string content, string source);
}

public class LiteratureSearchService : ILiteratureSearchService
{
    private readonly AppDbContext   _context;
    private readonly IOllamaService _ollama;
    private readonly ILogger<LiteratureSearchService> _logger;

    // Built-in clinical knowledge base
    private static readonly List<(string Title, string Content, string Source)> BuiltInGuidelines = new()
    {
        (
            "Diabetes Management Guidelines (ADA 2024)",
            "Glycemic targets: HbA1c <7% for most adults with type 2 diabetes. " +
            "First-line therapy: Metformin unless contraindicated (eGFR <30). " +
            "Add GLP-1 agonist or SGLT-2 inhibitor for cardiovascular/renal benefit. " +
            "Monitor HbA1c every 3 months until stable, then every 6 months. " +
            "Screen for retinopathy annually, nephropathy with urine ACR.",
            "ADA Standards of Medical Care 2024"
        ),
        (
            "Hypertension Management (JNC 8 / ESC 2023)",
            "Target BP <130/80 mmHg for most patients. " +
            "First-line: ACE inhibitors, ARBs, thiazide diuretics, or CCBs. " +
            "ACE inhibitor/ARB preferred in diabetic nephropathy. " +
            "Beta-blockers preferred post-MI or heart failure. " +
            "Lifestyle: DASH diet, reduce sodium to <2.3g/day, exercise 150min/week.",
            "ESC/ESH Hypertension Guidelines 2023"
        ),
        (
            "Community-Acquired Pneumonia (CAP) Management",
            "Mild CAP (outpatient): Amoxicillin 500mg TDS or Doxycycline 100mg BD x5 days. " +
            "Moderate CAP (inpatient): Beta-lactam + macrolide combination. " +
            "Severe CAP (ICU): IV beta-lactam + IV macrolide or fluoroquinolone. " +
            "Investigations: Chest X-ray, CBC, CRP, blood cultures if severe. " +
            "CURB-65 score guides admission decision.",
            "BTS CAP Guidelines 2023"
        ),
        (
            "Acute Coronary Syndrome (ACS) Management",
            "STEMI: Primary PCI within 90 minutes is gold standard. " +
            "Dual antiplatelet: Aspirin 300mg loading + P2Y12 inhibitor (Ticagrelor/Clopidogrel). " +
            "Anticoagulation: Heparin IV or Enoxaparin SC. " +
            "NSTEMI: Risk stratify with TIMI/GRACE score. " +
            "High-intensity statin (Atorvastatin 80mg) + beta-blocker + ACE inhibitor.",
            "ESC ACS Guidelines 2023"
        ),
        (
            "Skin Cancer Detection — Melanoma",
            "ABCDE rule: Asymmetry, irregular Border, multiple Colors, Diameter >6mm, Evolution. " +
            "Suspicious lesion: Urgency referral to dermatology within 2 weeks. " +
            "Biopsy: Excisional biopsy with 1-2mm margin is gold standard. " +
            "Staging: Breslow thickness determines prognosis. " +
            "Sentinel lymph node biopsy for Breslow >0.8mm.",
            "NICE NG14 Melanoma Guidelines 2023"
        ),
        (
            "Brain Tumour — Diagnosis and Referral",
            "Red flags: New onset seizures, progressive headache, focal neurology, personality change. " +
            "Initial investigation: MRI brain with contrast (CT if MRI unavailable). " +
            "Urgent 2-week wait referral for suspected primary brain tumour. " +
            "Glioblastoma (GBM): Surgical resection + Temozolomide + radiotherapy. " +
            "Dexamethasone for perilesional oedema.",
            "NICE NG99 Brain Tumours 2023"
        ),
        (
            "Drug Interaction Reference — Common High-Risk Pairs",
            "Warfarin + NSAIDs: Increased bleeding risk — avoid or monitor INR closely. " +
            "Metformin + contrast media: Hold 48h before/after IV contrast (eGFR dependent). " +
            "ACE inhibitor + potassium-sparing diuretic: Risk of hyperkalemia — monitor K+. " +
            "Statins + fibrates: Risk of myopathy — monitor CK. " +
            "SSRIs + MAOIs: Serotonin syndrome — 14-day washout required. " +
            "Simvastatin + amiodarone: Myopathy risk — max simvastatin 20mg.",
            "BNF Drug Interactions 2024"
        ),
        (
            "Heart Failure Management (HFrEF)",
            "Foundational therapy: ACE inhibitor/ARB/ARNI + beta-blocker + MRA + SGLT2i. " +
            "Target: Optimise doses to reduce hospitalisation and mortality. " +
            "Loop diuretics for congestion (Furosemide). " +
            "ICD for EF <35% on optimal therapy. " +
            "CRT for EF <35% with LBBB QRS >150ms.",
            "ESC Heart Failure Guidelines 2023"
        )
    };

    public LiteratureSearchService(
        AppDbContext             context,
        IOllamaService           ollama,
        ILogger<LiteratureSearchService> logger)
    {
        _context = context;
        _ollama  = ollama;
        _logger  = logger;
    }

    public async Task<ApiResponse<List<LiteratureSearchResult>>> SearchAsync(
        LiteratureSearchRequest req, Guid doctorId)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return ApiResponse<List<LiteratureSearchResult>>.Fail("Search query cannot be empty.");

        _logger.LogInformation(
            "Literature search: '{Query}' (topK={TopK})", req.Query, req.TopK);

        var results = new List<LiteratureSearchResult>();

        // ── Step 1: Vector similarity search in DB (pgvector) ─────────────────
        try
        {
            var queryEmbedding = await _ollama.GenerateEmbeddingAsync(req.Query);
            if (queryEmbedding.Length > 0)
            {
                var embeddingStr = "[" + string.Join(",", queryEmbedding.Take(1536)) + "]";
                var dbResults = await _context.ChatbotMemories
                    .FromSqlRaw(
                        @"SELECT * FROM ""ChatbotMemories""
                          WHERE ""ContextType"" = 'guideline'
                          ORDER BY ""Embedding"" <=> {0}::vector
                          LIMIT {1}",
                        embeddingStr, req.TopK)
                    .ToListAsync();

                foreach (var mem in dbResults)
                {
                    results.Add(new LiteratureSearchResult
                    {
                        Title           = mem.ContextType,
                        Passage         = mem.SourceRef,
                        Source          = "MAPS Clinical Knowledge Base",
                        SimilarityScore = 0.85f
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pgvector search failed — using keyword fallback");
        }

        // ── Step 2: Keyword fallback on built-in guidelines ───────────────────
        if (results.Count < req.TopK)
        {
            var queryLower   = req.Query.ToLower();
            var queryKeywords = queryLower.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);

            var scored = BuiltInGuidelines
                .Select(g => new
                {
                    g.Title, g.Content, g.Source,
                    Score = queryKeywords
                        .Count(kw => g.Title.ToLower().Contains(kw) ||
                                     g.Content.ToLower().Contains(kw))
                })
                .Where(g => g.Score > 0)
                .OrderByDescending(g => g.Score)
                .Take(req.TopK - results.Count)
                .ToList();

            foreach (var g in scored)
            {
                results.Add(new LiteratureSearchResult
                {
                    Title           = g.Title,
                    Passage         = g.Content,
                    Source          = g.Source,
                    SimilarityScore = Math.Min((float)g.Score / 5f, 1.0f)
                });
            }
        }

        // ── Step 3: Synthesize with Ollama if results found ───────────────────
        if (results.Any())
        {
            try
            {
                var passages  = string.Join("\n\n",
                    results.Select(r => $"[{r.Source}]\n{r.Passage}"));
                var synthesis = await _ollama.GenerateResponseAsync(
                    "You are a clinical guidelines assistant. " +
                    "Synthesize the following retrieved passages into a concise, " +
                    "actionable clinical summary. Cite sources.",
                    $"Query: {req.Query}\n\nPassages:\n{passages}");

                // Add synthesized result as first item
                results.Insert(0, new LiteratureSearchResult
                {
                    Title           = "AI-Synthesized Summary",
                    Passage         = synthesis +
                        "\n\n⚠️ AI synthesis — verify against primary sources.",
                    Source          = "MAPS Literature AI",
                    SimilarityScore = 1.0f
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Literature synthesis via Ollama failed");
            }
        }

        if (!results.Any())
        {
            results.Add(new LiteratureSearchResult
            {
                Title           = "No Results Found",
                Passage         = $"No clinical guidelines found for '{req.Query}'. " +
                                   "Please try different search terms or consult " +
                                   "external resources like UpToDate, NICE, or PubMed.",
                Source          = "MAPS",
                SimilarityScore = 0f
            });
        }

        return ApiResponse<List<LiteratureSearchResult>>.Ok(
            results.Take(req.TopK).ToList());
    }

    public async Task IndexDocumentAsync(
        string title, string content, string source)
    {
        try
        {
            var embedding = await _ollama.GenerateEmbeddingAsync($"{title}\n{content}");
            if (embedding.Length == 0) return;

            var embStr = "[" + string.Join(",", embedding.Take(1536)) + "]";
            _context.ChatbotMemories.Add(new ChatbotMemory
            {
                DoctorId    = Guid.Empty, // System-level document
                Embedding   = embStr,
                ContextType = "guideline",
                SourceRef   = $"{title} | {source} | {content[..Math.Min(content.Length, 500)]}"
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Indexed literature document: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document: {Title}", title);
        }
    }
}
