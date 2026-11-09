using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace OpsMind.Core.Agent;

/// <summary>Вложения тикетов в S3-хранилище (SeaweedFS). Ключ объекта: «{номер тикета}/{имя файла}».</summary>
public sealed class AttachmentStore(AgentOptions options) : IDisposable {
    const int MaxCharsPerFile = 6000; // большие логи обрезаем, чтобы не переполнить контекст модели

    readonly AmazonS3Client _s3 = new(
        new BasicAWSCredentials(options.S3AccessKey, options.S3SecretKey),
        new AmazonS3Config {
            ServiceURL = options.S3Endpoint,
            ForcePathStyle = true, // bucket в пути, а не в поддомене — так работают MinIO/SeaweedFS
            AuthenticationRegion = "us-east-1",
            // AWS SDK v4 по умолчанию добавляет контрольные суммы, которые понимают не все S3-совместимые хранилища.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });

    public async Task<string> ReadForTicketAsync(string ticketId, CancellationToken ct = default) {
        var prefix = ticketId.Trim().ToUpperInvariant() + "/";
        var list = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.AttachmentsBucket, Prefix = prefix }, ct);
        if (list.S3Objects is not { Count: > 0 })
            return $"У тикета {ticketId} нет вложений.";

        var sb = new StringBuilder();
        foreach (var obj in list.S3Objects) {
            using var response = await _s3.GetObjectAsync(options.AttachmentsBucket, obj.Key, ct);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(ct);
            if (text.Length > MaxCharsPerFile)
                text = text[..MaxCharsPerFile] + "\n…(обрезано)";
            sb.AppendLine($"=== {obj.Key[prefix.Length..]} ===").AppendLine(text);
        }
        return sb.ToString();
    }

    /// <summary>Загружает папку вида data/attachments/{тикет}/{файл} в бакет (используется при ingestion).</summary>
    public async Task<int> UploadDirectoryAsync(string root, CancellationToken ct = default) {
        try {
            await _s3.PutBucketAsync(options.AttachmentsBucket, ct);
        } catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") {
        }

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (var file in files) {
            var key = Path.GetRelativePath(root, file).Replace('\\', '/');
            await _s3.PutObjectAsync(new PutObjectRequest { BucketName = options.AttachmentsBucket, Key = key, FilePath = file }, ct);
        }
        return files.Length;
    }

    public void Dispose() => _s3.Dispose();
}