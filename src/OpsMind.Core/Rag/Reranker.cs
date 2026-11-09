using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OpsMind.Core.Rag;

/// <summary>
/// Клиент reranker'а (TEI, модель bge-reranker-v2-m3).
///
/// Эмбеддинг-модель кодирует вопрос и фрагмент по отдельности и сравнивает векторы (bi-encoder) — быстро,
/// подходит для поиска по всей базе. Reranker читает пару «вопрос + фрагмент» целиком (cross-encoder):
/// видит, отвечает ли фрагмент именно на этот вопрос. Это точнее, но в разы дороже, поэтому
/// reranker переоценивает только 20 кандидатов, найденных обычным поиском.
/// </summary>
public sealed class Reranker(HttpClient http) {
    /// <summary>Возвращает кандидатов, отсортированных по оценке reranker'а (0..1), с этой оценкой в RerankScore.</summary>
    public async Task<IReadOnlyList<SearchHit>> RerankAsync(string query, IReadOnlyList<SearchHit> candidates, int topK, CancellationToken ct = default) {
        if (candidates.Count == 0)
            return candidates;

        var response = await http.PostAsJsonAsync("rerank", new RerankRequest(query, candidates.Select(c => c.Text).ToArray()), ct);
        response.EnsureSuccessStatusCode();
        var scores = await response.Content.ReadFromJsonAsync<RerankScore[]>(ct) ?? [];

        return scores
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .Select(s => candidates[s.Index] with { RerankScore = s.Score })
            .ToList();
    }

    sealed record RerankRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("texts")] string[] Texts);

    sealed record RerankScore(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("score")] float Score);
}