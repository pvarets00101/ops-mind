using OpsMind.Core.Agent;
using OpsMind.Core.Rag;

namespace OpsMind.Ingestion;

/// <summary>
/// Однократная индексация markdown-документов из папки в Qdrant.
/// На следующем шаге источником станет S3-бакет, а запуск — событие загрузки файла.
/// </summary>
public sealed class IndexingWorker(
    KnowledgeBase knowledgeBase,
    AttachmentStore attachments,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<IndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sourceDir = ResolveDirectory(configuration["Ingestion:SourceDir"] ?? "data/runbooks");
        logger.LogInformation("Индексация {Dir}", sourceDir);

        // Полная переиндексация: пересоздаём коллекцию, чтобы применить изменения схемы и убрать удалённые документы.
        await knowledgeBase.EnsureCollectionAsync(recreate: true, stoppingToken);

        var files = Directory.GetFiles(sourceDir, "*.md");
        var totalChunks = 0;
        var started = DateTime.UtcNow;

        foreach (var file in files)
        {
            var markdown = await File.ReadAllTextAsync(file, stoppingToken);
            var chunks = MarkdownChunker.Chunk(markdown, Path.GetFileName(file));
            await knowledgeBase.IndexDocumentAsync(chunks, stoppingToken);
            totalChunks += chunks.Count;
            logger.LogDebug("{File}: {Count} фрагментов", Path.GetFileName(file), chunks.Count);
        }

        logger.LogInformation("Готово: {Docs} документов, {Chunks} фрагментов за {Seconds:N1} с",
            files.Length, totalChunks, (DateTime.UtcNow - started).TotalSeconds);

        var attachmentsDir = ResolveDirectory(configuration["Ingestion:AttachmentsDir"] ?? "data/attachments");
        var uploaded = await attachments.UploadDirectoryAsync(attachmentsDir, stoppingToken);
        logger.LogInformation("Вложения: загружено {Count} файлов в S3", uploaded);

        lifetime.StopApplication();
    }

    // Относительный путь ищется вверх от текущей папки: dotnet run запускает процесс в папке проекта,
    // а data/ лежит в корне репозитория. В контейнере путь задаётся абсолютным.
    static string ResolveDirectory(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, path);
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException($"Папка {path} не найдена ни в текущей, ни в родительских папках");
    }
}
