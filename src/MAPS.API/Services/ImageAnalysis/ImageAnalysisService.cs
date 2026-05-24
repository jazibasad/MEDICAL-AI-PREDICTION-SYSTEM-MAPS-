using Microsoft.EntityFrameworkCore;
using MAPS.API.Data;
using MAPS.API.Data.Entities;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.API.Services.Storage;
using MAPS.ML.ImageAnalysis;
using MAPS.Shared.DTOs.Common;
using MAPS.Shared.Enums;

namespace MAPS.API.Services.ImageAnalysis;

public class ImageAnalysisResultDto
{
    public Guid    ImageId       { get; set; }
    public string  PrimaryLabel  { get; set; } = string.Empty;
    public float   Confidence    { get; set; }
    public bool    IsPositive    { get; set; }
    public string  Modality      { get; set; } = string.Empty;
    public string  ReportSummary { get; set; } = string.Empty;
    public string  ImageUrl      { get; set; } = string.Empty;
    public string  AiResultJson  { get; set; } = string.Empty;
    public DateTime AnalysedAt   { get; set; }
}

public interface IImageAnalysisService
{
    Task<ApiResponse<ImageAnalysisResultDto>> AnalyseAsync(
        IFormFile file, DiseaseType disease, Guid patientId, Guid doctorId);
    Task<ApiResponse<ImageAnalysisResultDto>> GetByIdAsync(Guid imageId, Guid doctorId);
    Task<ApiResponse<List<ImageAnalysisResultDto>>> GetByPatientAsync(
        Guid patientId, Guid doctorId);
}

public class ImageAnalysisService : IImageAnalysisService
{
    private readonly AppDbContext          _context;
    private readonly IAssignmentRepository _assignRepo;
    private readonly IMinioStorageService  _storage;
    private readonly IPneumoniaAnalyzer    _pneumonia;
    private readonly IBrainTumourAnalyzer  _brainTumour;
    private readonly ISkinCancerAnalyzer   _skinCancer;
    private readonly IAuditRepository      _auditRepo;
    private readonly ILogger<ImageAnalysisService> _logger;

    public ImageAnalysisService(
        AppDbContext          context,
        IAssignmentRepository assignRepo,
        IMinioStorageService  storage,
        IPneumoniaAnalyzer    pneumonia,
        IBrainTumourAnalyzer  brainTumour,
        ISkinCancerAnalyzer   skinCancer,
        IAuditRepository      auditRepo,
        ILogger<ImageAnalysisService> logger)
    {
        _context     = context;
        _assignRepo  = assignRepo;
        _storage     = storage;
        _pneumonia   = pneumonia;
        _brainTumour = brainTumour;
        _skinCancer  = skinCancer;
        _auditRepo   = auditRepo;
        _logger      = logger;
    }

    public async Task<ApiResponse<ImageAnalysisResultDto>> AnalyseAsync(
        IFormFile file, DiseaseType disease, Guid patientId, Guid doctorId)
    {
        // Validate assignment
        if (!await _assignRepo.IsPatientAssignedToDoctor(patientId, doctorId))
            return ApiResponse<ImageAnalysisResultDto>.Fail(
                "Patient not assigned to you.");

        // Validate file
        var allowed = new[] { "image/jpeg","image/png","image/dicom","application/dicom" };
        if (!allowed.Contains(file.ContentType.ToLower()))
            return ApiResponse<ImageAnalysisResultDto>.Fail(
                "Only JPEG, PNG, or DICOM images are accepted.");

        if (file.Length > 20 * 1024 * 1024)
            return ApiResponse<ImageAnalysisResultDto>.Fail(
                "Image file must not exceed 20MB.");

        // Read image bytes
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var imageBytes = ms.ToArray();

        // Route to correct ONNX analyzer
        ImageAnalysisResult analysisResult;
        string modalityStr;

        try
        {
            (analysisResult, modalityStr) = disease switch
            {
                DiseaseType.Pneumonia   =>
                    (_pneumonia.Analyse(imageBytes),   "Chest X-Ray"),
                DiseaseType.BrainTumour =>
                    (_brainTumour.Analyse(imageBytes), "Brain MRI"),
                DiseaseType.SkinCancer  =>
                    (_skinCancer.Analyse(imageBytes),  "Skin Lesion"),
                _ => throw new ArgumentException(
                    $"Disease {disease} does not support image input.")
            };
        }
        catch (ArgumentException ex)
        {
            return ApiResponse<ImageAnalysisResultDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ONNX inference failed for {Disease}", disease);
            return ApiResponse<ImageAnalysisResultDto>.Fail(
                "Image analysis failed. Please try again.");
        }

        // Upload image to MinIO (AES-256 at rest via MinIO config)
        ms.Position = 0;
        var objectKey = $"images/{patientId}/{disease}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var imageUrl  = await _storage.UploadAsync(objectKey, ms, file.ContentType);

        // Serialize AI result to JSON for DB storage
        var aiResultJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            analysisResult.PrimaryLabel,
            analysisResult.Confidence,
            analysisResult.IsPositive,
            analysisResult.ReportSummary,
            analysisResult.DetectedRegions,
            analysisResult.AnalysedAt
        });

        // Persist to DB
        var doctorProfile = await _context.DoctorProfiles
            .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

        var medicalImage = new MedicalImage
        {
            PatientId  = patientId,
            DoctorId   = doctorId,
            Modality   = modalityStr,
            FilePath   = objectKey,
            AiResult   = aiResultJson,
            UploadedAt = DateTime.UtcNow
        };

        _context.MedicalImages.Add(medicalImage);
        await _context.SaveChangesAsync();

        await _auditRepo.LogAsync(new AuditLog
        {
            UserId     = doctorId,
            Action     = "IMAGE_ANALYSED",
            EntityType = "MedicalImage",
            EntityId   = medicalImage.ImageId.ToString(),
            NewValues  = $"{{\"disease\":\"{disease}\",\"positive\":{analysisResult.IsPositive.ToString().ToLower()},\"confidence\":{analysisResult.Confidence:F4}}}"
        });

        _logger.LogInformation(
            "Image analysis complete: {Disease} | Positive={Pos} | Confidence={Conf:F2}",
            disease, analysisResult.IsPositive, analysisResult.Confidence);

        return ApiResponse<ImageAnalysisResultDto>.Ok(new ImageAnalysisResultDto
        {
            ImageId       = medicalImage.ImageId,
            PrimaryLabel  = analysisResult.PrimaryLabel,
            Confidence    = analysisResult.Confidence,
            IsPositive    = analysisResult.IsPositive,
            Modality      = modalityStr,
            ReportSummary = analysisResult.ReportSummary,
            ImageUrl      = imageUrl,
            AiResultJson  = aiResultJson,
            AnalysedAt    = analysisResult.AnalysedAt
        });
    }

    public async Task<ApiResponse<ImageAnalysisResultDto>> GetByIdAsync(
        Guid imageId, Guid doctorId)
    {
        var img = await _context.MedicalImages
            .FirstOrDefaultAsync(i => i.ImageId == imageId && i.DoctorId == doctorId);

        if (img is null)
            return ApiResponse<ImageAnalysisResultDto>.Fail("Image not found.");

        var url = await _storage.GetPresignedUrlAsync(img.FilePath);

        return ApiResponse<ImageAnalysisResultDto>.Ok(new ImageAnalysisResultDto
        {
            ImageId      = img.ImageId,
            Modality     = img.Modality,
            ImageUrl     = url,
            AiResultJson = img.AiResult,
            AnalysedAt   = img.UploadedAt
        });
    }

    public async Task<ApiResponse<List<ImageAnalysisResultDto>>> GetByPatientAsync(
        Guid patientId, Guid doctorId)
    {
        var images = await _context.MedicalImages
            .Where(i => i.PatientId == patientId && i.DoctorId == doctorId)
            .OrderByDescending(i => i.UploadedAt)
            .ToListAsync();

        var dtos = new List<ImageAnalysisResultDto>();
        foreach (var img in images)
        {
            var url = await _storage.GetPresignedUrlAsync(img.FilePath);
            dtos.Add(new ImageAnalysisResultDto
            {
                ImageId      = img.ImageId,
                Modality     = img.Modality,
                ImageUrl     = url,
                AiResultJson = img.AiResult,
                AnalysedAt   = img.UploadedAt
            });
        }

        return ApiResponse<List<ImageAnalysisResultDto>>.Ok(dtos);
    }
}
