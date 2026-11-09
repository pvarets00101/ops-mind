namespace OpsMind.Core.Rag;

public sealed class RagOptions {
    public const string SectionName = "Rag";

    public string QdrantHost { get; set; } = "localhost";

    /// <summary>gRPC-порт Qdrant (REST — 6333).</summary>
    public int QdrantPort { get; set; } = 6334;

    public string Collection { get; set; } = "runbooks";

    public SearchMode SearchMode { get; set; } = SearchMode.Hybrid;

    /// <summary>
    /// Вес BM25 в гибридном поиске относительно dense (вес 1). BM25 в одиночку заметно слабее,
    /// поэтому при равных весах он «перетягивает» правильные ответы dense вниз. Подбирается через RagEval.
    /// </summary>
    public double SparseWeight { get; set; } = 0.5;

    /// <summary>Адрес TEI с reranker'ом (http://localhost:8081/). Пусто — reranker выключен.</summary>
    public string? RerankerUrl { get; set; }

    /// <summary>Сколько кандидатов из поиска переоценивает reranker.</summary>
    public int RerankCandidates { get; set; } = 20;

    /// <summary>Сколько фрагментов передаётся в контекст LLM.</summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Минимальная косинусная (dense) близость лучшего фрагмента. Ниже — считаем, что в базе нет ответа,
    /// и не вызываем LLM. Значение подбирается по data/eval (tools/OpsMind.RagEval).
    /// </summary>
    public float MinScore { get; set; } = 0.48f;

    /// <summary>
    /// Порог отказа по оценке reranker'а (используется вместо MinScore, если reranker включён).
    /// На eval-наборах: вопросы из базы ≥ 0.029, вне базы ≤ 0.008 — классы не пересекаются, в отличие от косинуса.
    /// </summary>
    public float RerankMinScore { get; set; } = 0.02f;
}