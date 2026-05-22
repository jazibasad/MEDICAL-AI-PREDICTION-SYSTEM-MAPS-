using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Services.Storage;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Services.Reports;

public interface IReportService
{
    Task<ApiResponse<string>> GeneratePatientSummaryAsync(Guid patientId, Guid doctorId);
    Task<ApiResponse<string>> GeneratePredictionReportAsync(Guid predictionId, Guid doctorId);
    Task<ApiResponse<string>> GenerateConsultationReportAsync(Guid patientId, Guid doctorId, string consultationNotes);
}

public class ReportService : IReportService
{
    private readonly AppDbContext         _context;
    private readonly IMinioStorageService _storage;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        AppDbContext           context,
        IMinioStorageService   storage,
        ILogger<ReportService> logger)
    {
        _context = context;
        _storage = storage;
        _logger  = logger;

        // Set QuestPDF license (Community = free)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Patient Health Summary ────────────────────────────────────────────────
    public async Task<ApiResponse<string>> GeneratePatientSummaryAsync(
        Guid patientId, Guid doctorId)
    {
        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientId == patientId);
        if (patient is null)
            return ApiResponse<string>.Fail("Patient not found.");

        var doctor = await _context.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

        var predictions = await _context.AIPredictions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        var latestRisk = await _context.RiskAssessments
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CalculatedAt)
            .FirstOrDefaultAsync();

        var prescriptions = await _context.Prescriptions
            .Where(p => p.PatientId == patientId && p.Status == "Active")
            .ToListAsync();

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MAPS — Medical AI Prediction System")
                                .FontSize(16).Bold().FontColor(Color.FromHex("1B2A4A"));
                            c.Item().Text("Patient Health Summary Report")
                                .FontSize(12).FontColor(Color.FromHex("6B7280"));
                        });
                        row.ConstantItem(120).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Generated: {DateTime.Now:dd MMM yyyy}")
                                .FontSize(9).FontColor(Color.FromHex("6B7280"));
                            c.Item().AlignRight().Text($"Doctor: {doctor?.User?.FullName ?? "N/A"}")
                                .FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(8).LineHorizontal(2)
                        .LineColor(Color.FromHex("2563EB"));
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    // Patient Info
                    col.Item().PaddingTop(12).Background(Color.FromHex("DBEAFE"))
                        .Padding(10).Column(info =>
                    {
                        info.Item().Text("Patient Information")
                            .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));
                        info.Item().PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Name: {patient.User.FullName}").FontSize(11);
                                c.Item().Text($"Email: {patient.User.Email}").FontSize(11);
                                c.Item().Text($"Blood Group: {patient.BloodGroup}").FontSize(11);
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"DOB: {patient.DateOfBirth?.ToString("dd MMM yyyy") ?? "N/A"}").FontSize(11);
                                c.Item().Text($"Emergency Contact: {patient.EmergencyContact}").FontSize(11);
                                c.Item().Text($"Risk Score: {latestRisk?.RiskScore:F1} ({latestRisk?.UrgencyTier})").FontSize(11);
                            });
                        });
                    });

                    // AI Predictions
                    if (predictions.Any())
                    {
                        col.Item().Text("Recent AI Predictions")
                            .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                            });

                            t.Header(h =>
                            {
                                foreach (var hdr in new[] { "Disease", "Diagnosis", "Confidence", "Date" })
                                    h.Cell().Background(Color.FromHex("1B2A4A"))
                                        .Padding(6).Text(hdr).FontColor(Colors.White).Bold().FontSize(10);
                            });

                            foreach (var (pred, idx) in predictions.Select((p, i) => (p, i)))
                            {
                                var bg = idx % 2 == 0 ? Color.FromHex("F3F4F6") : Colors.White;
                                t.Cell().Background(bg).Padding(5).Text(pred.DiseaseType.ToString()).FontSize(10);
                                t.Cell().Background(bg).Padding(5).Text(pred.PrimaryDiagnosis).FontSize(10);
                                t.Cell().Background(bg).Padding(5)
                                    .Text($"{pred.Confidence * 100:F1}%").FontSize(10);
                                t.Cell().Background(bg).Padding(5)
                                    .Text(pred.CreatedAt.ToString("dd MMM")).FontSize(10);
                            }
                        });
                    }

                    // Active Prescriptions
                    if (prescriptions.Any())
                    {
                        col.Item().Text("Active Prescriptions")
                            .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));
                        foreach (var rx in prescriptions)
                        {
                            col.Item().Background(Color.FromHex("F0FDF4"))
                                .Padding(8).Column(r =>
                            {
                                r.Item().Text($"Medications: {rx.Medications}").FontSize(10);
                                r.Item().Text($"Expires: {rx.ExpiresAt?.ToString("dd MMM yyyy") ?? "—"}").FontSize(10);
                            });
                        }
                    }

                    // Disclaimer
                    col.Item().PaddingTop(20).Background(Color.FromHex("FEF3C7"))
                        .Padding(8).Text(
                            "⚠️ DISCLAIMER: This report is generated by the MAPS AI system for clinical " +
                            "decision support only. All AI predictions must be validated by a qualified " +
                            "medical professional before clinical action.")
                        .FontSize(9).FontColor(Color.FromHex("92400E"));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("MAPS Medical AI Prediction System · Confidential · Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                }).FontSize(9).FontColor(Color.FromHex("6B7280"));
            });
        }).GeneratePdf();

        // Upload to MinIO
        var objectKey = $"reports/patient-summary/{patientId}/{Guid.NewGuid()}.pdf";
        using var ms  = new MemoryStream(pdfBytes);
        var url       = await _storage.UploadAsync(objectKey, ms, "application/pdf");

        _logger.LogInformation("Generated patient summary report for {PatientId}", patientId);
        return ApiResponse<string>.Ok(url, "Report generated successfully.");
    }

    // ── Prediction Report ─────────────────────────────────────────────────────
    public async Task<ApiResponse<string>> GeneratePredictionReportAsync(
        Guid predictionId, Guid doctorId)
    {
        var prediction = await _context.AIPredictions
            .Include(p => p.Patient).ThenInclude(pt => pt.User)
            .Include(p => p.DifferentialDiagnoses)
            .FirstOrDefaultAsync(p => p.PredictionId == predictionId &&
                                       p.DoctorId     == doctorId);

        if (prediction is null)
            return ApiResponse<string>.Fail("Prediction not found or access denied.");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("MAPS — AI Prediction Report")
                        .FontSize(18).Bold().FontColor(Color.FromHex("1B2A4A"));
                    col.Item().PaddingTop(4).LineHorizontal(2)
                        .LineColor(Color.FromHex("2563EB"));
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().PaddingTop(10);

                    // Summary box
                    col.Item().Background(Color.FromHex("EFF6FF")).Padding(12).Column(s =>
                    {
                        s.Item().Text($"Patient: {prediction.Patient.User.FullName}")
                            .Bold().FontSize(13);
                        s.Item().Text($"Disease Type: {prediction.DiseaseType}").FontSize(11);
                        s.Item().Text($"Primary Diagnosis: {prediction.PrimaryDiagnosis}")
                            .FontSize(11);
                        s.Item().Text($"Confidence: {prediction.Confidence * 100:F1}%")
                            .FontSize(11).FontColor(Color.FromHex("059669"));
                        s.Item().Text($"Input Modality: {prediction.InputModality}").FontSize(11);
                        s.Item().Text($"Generated: {prediction.CreatedAt:dd MMM yyyy HH:mm}")
                            .FontSize(11);
                    });

                    // Doctor Interpretation
                    if (!string.IsNullOrEmpty(prediction.DoctorInterpretation))
                    {
                        col.Item().Text("Doctor's Interpretation")
                            .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));
                        col.Item().Background(Color.FromHex("F0FDF4")).Padding(10)
                            .Text(prediction.DoctorInterpretation).FontSize(11);
                    }

                    // Differential Diagnoses
                    if (prediction.DifferentialDiagnoses.Any())
                    {
                        col.Item().Text("Differential Diagnoses")
                            .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(5);
                            });

                            t.Header(h =>
                            {
                                foreach (var hdr in new[] { "Rank", "Condition", "Probability", "Reasoning" })
                                    h.Cell().Background(Color.FromHex("5048CF"))
                                        .Padding(6).Text(hdr)
                                        .FontColor(Colors.White).Bold().FontSize(10);
                            });

                            foreach (var ddx in prediction.DifferentialDiagnoses
                                         .OrderBy(d => d.RankPosition))
                            {
                                var bg = ddx.RankPosition % 2 == 0
                                    ? Color.FromHex("F3F4F6") : Colors.White;
                                t.Cell().Background(bg).Padding(5)
                                    .Text($"#{ddx.RankPosition}").FontSize(10);
                                t.Cell().Background(bg).Padding(5)
                                    .Text(ddx.Condition).FontSize(10).Bold();
                                t.Cell().Background(bg).Padding(5)
                                    .Text($"{ddx.Probability * 100:F1}%").FontSize(10);
                                t.Cell().Background(bg).Padding(5)
                                    .Text(ddx.ReasoningChain.Length > 120
                                        ? ddx.ReasoningChain[..120] + "..."
                                        : ddx.ReasoningChain).FontSize(9);
                            }
                        });
                    }

                    // Disclaimer
                    col.Item().PaddingTop(16).Background(Color.FromHex("FEF3C7"))
                        .Padding(8).Text(
                            "⚠️ AI DISCLAIMER: This prediction is generated by ML.NET/ONNX models " +
                            "for clinical decision support only. Final diagnosis must be made by a " +
                            "qualified medical professional. Confidence scores represent model " +
                            "probability, not clinical certainty.")
                        .FontSize(9).FontColor(Color.FromHex("92400E"));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("MAPS · Confidential Medical Report · Page ");
                    t.CurrentPageNumber();
                }).FontSize(9).FontColor(Color.FromHex("6B7280"));
            });
        }).GeneratePdf();

        var objectKey = $"reports/predictions/{predictionId}/{Guid.NewGuid()}.pdf";
        using var ms  = new MemoryStream(pdfBytes);
        var url       = await _storage.UploadAsync(objectKey, ms, "application/pdf");

        return ApiResponse<string>.Ok(url, "Prediction report generated.");
    }

    // ── Consultation Report ───────────────────────────────────────────────────
    public async Task<ApiResponse<string>> GenerateConsultationReportAsync(
        Guid patientId, Guid doctorId, string consultationNotes)
    {
        var patient = await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientId == patientId);
        var doctor  = await _context.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

        if (patient is null || doctor is null)
            return ApiResponse<string>.Fail("Patient or doctor not found.");

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("MAPS — Consultation Report")
                        .FontSize(18).Bold().FontColor(Color.FromHex("1B2A4A"));
                    col.Item().PaddingTop(4).LineHorizontal(2)
                        .LineColor(Color.FromHex("10B981"));
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Patient").Bold().FontSize(10)
                                .FontColor(Color.FromHex("6B7280"));
                            c.Item().Text(patient.User.FullName).FontSize(13).Bold();
                            c.Item().Text(patient.User.Email).FontSize(10);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Doctor").Bold().FontSize(10)
                                .FontColor(Color.FromHex("6B7280"));
                            c.Item().Text(doctor.User.FullName).FontSize(13).Bold();
                            c.Item().Text(doctor.Specialization).FontSize(10);
                        });
                        r.ConstantItem(100).Column(c =>
                        {
                            c.Item().Text("Date").Bold().FontSize(10)
                                .FontColor(Color.FromHex("6B7280"));
                            c.Item().Text(DateTime.Now.ToString("dd MMM yyyy")).FontSize(11);
                        });
                    });

                    col.Item().LineHorizontal(1).LineColor(Color.FromHex("E5E7EB"));

                    col.Item().Text("Consultation Notes")
                        .Bold().FontSize(13).FontColor(Color.FromHex("1B2A4A"));
                    col.Item().Background(Color.FromHex("F9FAFB")).Padding(12)
                        .Text(consultationNotes).FontSize(11);

                    col.Item().PaddingTop(16).Background(Color.FromHex("FEF3C7"))
                        .Padding(8).Text(
                            "⚠️ This consultation report is for medical record purposes only.")
                        .FontSize(9).FontColor(Color.FromHex("92400E"));
                });

                page.Footer().AlignCenter()
                    .Text($"MAPS · Consultation Report · {DateTime.Now:dd MMM yyyy}")
                    .FontSize(9).FontColor(Color.FromHex("6B7280"));
            });
        }).GeneratePdf();

        var objectKey = $"reports/consultations/{patientId}/{Guid.NewGuid()}.pdf";
        using var ms  = new MemoryStream(pdfBytes);
        var url       = await _storage.UploadAsync(objectKey, ms, "application/pdf");

        return ApiResponse<string>.Ok(url, "Consultation report generated.");
    }
}
