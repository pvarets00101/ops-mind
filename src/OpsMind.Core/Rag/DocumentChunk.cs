namespace OpsMind.Core.Rag;

/// <summary>Фрагмент документа, который индексируется в векторной БД.</summary>
/// <param name="DocId">Идентификатор документа из front matter (RB-010).</param>
/// <param name="Section">Заголовок раздела второго уровня, к которому относится фрагмент.</param>
/// <param name="Index">Порядковый номер фрагмента внутри документа.</param>
/// <param name="Text">Текст для эмбеддинга: заголовок документа и раздела + содержимое.</param>
public sealed record DocumentChunk(
    string DocId,
    string Title,
    string Category,
    string Team,
    string Section,
    int Index,
    string Text,
    string Source);

/// <summary>Результат поиска по базе знаний.</summary>
/// <param name="Score">Итоговая оценка в выбранном режиме: косинус (dense), BM25 (sparse) или RRF (hybrid).</param>
/// <param name="DenseScore">Косинусная близость, если фрагмент найден dense-поиском.</param>
/// <param name="RerankScore">Оценка reranker'а (0..1), если он включён.</param>
public sealed record SearchHit(
    string DocId, string Title, string Section, string Text, float Score, float? DenseScore, float? RerankScore = null);

public enum SearchMode {
    /// <summary>Только векторный (семантический) поиск.</summary>
    Dense,

    /// <summary>Только лексический BM25.</summary>
    Sparse,

    /// <summary>Оба поиска, результаты объединяются методом Reciprocal Rank Fusion.</summary>
    Hybrid,
}