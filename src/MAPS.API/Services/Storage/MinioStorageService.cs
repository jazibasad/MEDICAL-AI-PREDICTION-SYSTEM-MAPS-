using Minio;
using Minio.DataModel.Args;

namespace MAPS.API.Services.Storage;

public interface IMinioStorageService
{
    Task<string> UploadAsync(string objectKey, Stream data, string contentType);
    Task<Stream> DownloadAsync(string objectKey);
    Task DeleteAsync(string objectKey);
    Task<string> GetPresignedUrlAsync(string objectKey, int expirySeconds = 3600);
    Task EnsureBucketExistsAsync();
}

public class MinioStorageService : IMinioStorageService
{
    private readonly IMinioClient _minio;
    private readonly string       _bucketName;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IConfiguration config, ILogger<MinioStorageService> logger)
    {
        _logger     = logger;
        _bucketName = config["MinIO:BucketName"] ?? "maps-storage";

        _minio = new MinioClient()
            .WithEndpoint(config["MinIO:Endpoint"] ?? "minio:9000")
            .WithCredentials(
                config["MinIO:AccessKey"] ?? "minioadmin",
                config["MinIO:SecretKey"] ?? "minioadmin")
            .WithSSL(config.GetValue<bool>("MinIO:UseSSL"))
            .Build();
    }

    public async Task EnsureBucketExistsAsync()
    {
        var exists = await _minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName));
        if (!exists)
        {
            await _minio.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName));
            _logger.LogInformation("Created MinIO bucket: {Bucket}", _bucketName);
        }
    }

    public async Task<string> UploadAsync(string objectKey, Stream data, string contentType)
    {
        await EnsureBucketExistsAsync();

        await _minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType));

        _logger.LogInformation("Uploaded {Key} to MinIO", objectKey);
        return await GetPresignedUrlAsync(objectKey);
    }

    public async Task<Stream> DownloadAsync(string objectKey)
    {
        var ms = new MemoryStream();
        await _minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(ms)));
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string objectKey)
    {
        await _minio.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey));
        _logger.LogInformation("Deleted {Key} from MinIO", objectKey);
    }

    public async Task<string> GetPresignedUrlAsync(string objectKey, int expirySeconds = 3600)
    {
        return await _minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds));
    }
}
