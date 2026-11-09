using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace OpsMind.Core.Rag;

/// <summary>
/// База знаний поверх Qdrant. У каждой точки два именованных вектора:
/// «dense» — эмбеддинг bge-m3 (смысл), «sparse» — BM25 (точные слова).
/// </summary>
public sealed class KnowledgeBase(
    QdrantClient qdrant,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    Reranker reranker,
    RagOptions options)
{
    const string Dense = "dense";
    const string Sparse = "sparse";

    /// <summary>Сколько кандидатов берёт каждый поиск перед слиянием в гибридном режиме.</summary>
    const int CandidatesPerSearch = 20;

    /// <summary>Константа RRF: сглаживает разницу между первыми местами (стандартное значение — 60).</summary>
    const int RrfK = 60;

    public async Task EnsureCollectionAsync(bool recreate = false, CancellationToken ct = default)
    {
        var exists = await qdrant.CollectionExistsAsync(options.Collection, ct);
        if (exists && !recreate)
            return;
        if (exists)
            await qdrant.DeleteCollectionAsync(options.Collection, cancellationToken: ct);

        // Размерность берём у самой модели эмбеддингов, чтобы не держать её в конфиге.
        var probe = await embedder.GenerateVectorAsync("probe", cancellationToken: ct);
        await qdrant.CreateCollectionAsync(
            options.Collection,
            vectorsConfig: new VectorParamsMap
            {
                Map = { [Dense] = new VectorParams { Size = (ulong)probe.Length, Distance = Distance.Cosine } },
            },
            sparseVectorsConfig: new SparseVectorConfig
            {
                // Idf: Qdrant сам считает, насколько слово редкое в коллекции — недостающая часть формулы BM25.
                Map = { [Sparse] = new SparseVectorParams { Modifier = Modifier.Idf } },
            },
            cancellationToken: ct);
        await qdrant.CreatePayloadIndexAsync(options.Collection, "doc_id", cancellationToken: ct);
    }

    /// <summary>Переиндексирует документ целиком: старые фрагменты удаляются, новые записываются.</summary>
    public async Task IndexDocumentAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0)
            return;

        var docId = chunks[0].DocId;
        await qdrant.DeleteAsync(options.Collection, MatchKeyword("doc_id", docId), cancellationToken: ct);

        var embeddings = await embedder.GenerateAsync(chunks.Select(c => c.Text), cancellationToken: ct);
        var points = chunks.Zip(embeddings, (chunk, embedding) =>
        {
            var (values, indices) = Bm25Encoder.EncodeDocument(chunk.Text);
            return new PointStruct
            {
                Id = StableId($"{chunk.DocId}:{chunk.Index}"),
                Vectors = new Dictionary<string, Vector>
                {
                    [Dense] = embedding.Vector.ToArray(),
                    [Sparse] = (values, indices),
                },
                Payload =
                {
                    ["doc_id"] = chunk.DocId,
                    ["title"] = chunk.Title,
                    ["category"] = chunk.Category,
                    ["team"] = chunk.Team,
                    ["section"] = chunk.Section,
                    ["chunk_index"] = chunk.Index,
                    ["text"] = chunk.Text,
                    ["source"] = chunk.Source,
                },
            };
        }).ToList();

        await qdrant.UpsertAsync(options.Collection, points, cancellationToken: ct);
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default) =>
        SearchAsync(query, topK, options.SearchMode, rerank: options.RerankerUrl is not null, ct);

    /// <summary>Поиск: при rerank = true берётся RerankCandidates кандидатов, и reranker выбирает из них topK.</summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query, int topK, SearchMode mode, bool rerank, CancellationToken ct = default)
    {
        if (!rerank)
            return await RetrieveAsync(query, topK, mode, ct);

        var candidates = await RetrieveAsync(query, Math.Max(topK, options.RerankCandidates), mode, ct);
        return await reranker.RerankAsync(query, candidates, topK, ct);
    }

    async Task<IReadOnlyList<SearchHit>> RetrieveAsync(string query, int topK, SearchMode mode, CancellationToken ct)
    {
        switch (mode)
        {
            case SearchMode.Dense:
                return await DenseSearchAsync(query, topK, ct);
            case SearchMode.Sparse:
                return await SparseSearchAsync(query, topK, ct);
        }

        var candidates = Math.Max(topK, CandidatesPerSearch);
        var denseTask = DenseSearchAsync(query, candidates, ct);
        var sparseTask = SparseSearchAsync(query, candidates, ct);
        return ReciprocalRankFusion([(await denseTask, 1.0), (await sparseTask, options.SparseWeight)], topK);
    }

    async Task<IReadOnlyList<SearchHit>> DenseSearchAsync(string query, int limit, CancellationToken ct)
    {
        var vector = await embedder.GenerateVectorAsync(query, cancellationToken: ct);
        var points = await qdrant.QueryAsync(
            options.Collection, query: vector.ToArray(), usingVector: Dense,
            limit: (ulong)limit, payloadSelector: true, cancellationToken: ct);
        return points.Select(p => ToHit(p, denseScore: p.Score)).ToList();
    }

    async Task<IReadOnlyList<SearchHit>> SparseSearchAsync(string query, int limit, CancellationToken ct)
    {
        var (values, indices) = Bm25Encoder.EncodeQuery(query);
        if (values.Length == 0)
            return [];

        var points = await qdrant.QueryAsync(
            options.Collection, query: (values, indices), usingVector: Sparse,
            limit: (ulong)limit, payloadSelector: true, cancellationToken: ct);
        return points.Select(p => ToHit(p, denseScore: null)).ToList();
    }

    /// <summary>
    /// Reciprocal Rank Fusion: каждый фрагмент получает сумму weight / (k + место) по всем спискам.
    /// Оценки разных поисков несопоставимы (косинус 0..1 против BM25 0..∞), поэтому сливаем по местам, а не по оценкам.
    /// </summary>
    static IReadOnlyList<SearchHit> ReciprocalRankFusion((IReadOnlyList<SearchHit> Hits, double Weight)[] lists, int topK)
    {
        var fused = new Dictionary<string, (SearchHit Hit, double Score, float? Dense)>();
        foreach (var (list, weight) in lists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var key = $"{hit.DocId}:{hit.Section}:{hit.Text.GetHashCode()}";
                var current = fused.GetValueOrDefault(key, (hit, 0, null));
                fused[key] = (current.Hit, current.Score + weight / (RrfK + rank + 1), current.Dense ?? hit.DenseScore);
            }
        }

        return fused.Values
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Hit with { Score = (float)x.Score, DenseScore = x.Dense })
            .ToList();
    }

    static SearchHit ToHit(ScoredPoint p, float? denseScore) => new(
        p.Payload["doc_id"].StringValue,
        p.Payload["title"].StringValue,
        p.Payload["section"].StringValue,
        p.Payload["text"].StringValue,
        p.Score,
        denseScore);

    // Детерминированный id: повторная индексация перезаписывает те же точки, а не плодит дубликаты.
    static Guid StableId(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));
}
