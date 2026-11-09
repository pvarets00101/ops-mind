using System.Text;
using System.Text.RegularExpressions;

namespace OpsMind.Core.Rag;

/// <summary>
/// Превращает текст в разреженный (sparse) вектор для лексического поиска BM25.
///
/// Разреженный вектор хранит только ненулевые элементы: индекс = хеш слова, значение = вес слова в тексте.
/// BM25 = IDF(слово) × TF-часть. TF-часть считаем здесь, а IDF (насколько слово редкое во всей коллекции)
/// Qdrant считает сам при поиске — для этого у коллекции включён модификатор Idf.
/// </summary>
public static partial class Bm25Encoder {
    const double K1 = 1.2; // насыщение: 10 повторов слова не в 10 раз важнее одного
    const double B = 0.75; // штраф за длину: в длинном тексте каждое слово весит меньше

    /// <summary>Средняя длина фрагмента в токенах. Для data/runbooks — около 90, точность не критична.</summary>
    public const double AvgDocLength = 90;

    /// <summary>Вектор документа (фрагмента): веса BM25 с учётом частоты слова и длины текста.</summary>
    public static (float[] Values, uint[] Indices) EncodeDocument(string text) {
        var tokens = Tokenize(text);
        var length = tokens.Count;
        var tf = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        return Build(tf.Select(kv => {
            var weight = kv.Value * (K1 + 1) / (kv.Value + K1 * (1 - B + B * length / AvgDocLength));
            return (kv.Key, (float)weight);
        }));
    }

    /// <summary>Вектор запроса: каждое уникальное слово с весом 1, «важность» добавит IDF на стороне Qdrant.</summary>
    public static (float[] Values, uint[] Indices) EncodeQuery(string text) =>
        Build(Tokenize(text).Distinct().Select(t => (t, 1f)));

    /// <summary>
    /// Токенизация: нижний регистр, слова и «составные» токены (169.254, pg_stat_activity, ingress-nginx)
    /// целиком, плюс их части. Стоп-слова отбрасываются, длинные русские слова грубо стеммируются.
    /// </summary>
    public static List<string> Tokenize(string text) {
        var result = new List<string>();
        foreach (Match m in TokenRegex().Matches(text.ToLowerInvariant().Replace('ё', 'е'))) {
            var token = m.Value;
            AddToken(result, token);

            // Составной токен дополнительно разбиваем на части: «id.corp.local» найдётся и по «corp».
            if (token.IndexOfAny(['.', '-', '_']) >= 0) {
                foreach (var part in token.Split('.', '-', '_'))
                    AddToken(result, part);
            }
        }
        return result;
    }

    static void AddToken(List<string> result, string token) {
        if (token.Length < 2 && !char.IsDigit(token[0]))
            return;
        if (StopWords.Contains(token))
            return;
        result.Add(Stem(token));
    }

    /// <summary>
    /// Простейший стемминг для русского: обрезаем длинные слова до 6 букв.
    /// «блокировка», «блокируется», «блокировки» → «блокир». Грубо, но для BM25 по регламентам достаточно;
    /// полноценная альтернатива — алгоритм Snowball (стеммер Портера для русского).
    /// </summary>
    static string Stem(string token) =>
        token.Length > 6 && token.All(c => c is >= 'а' and <= 'я') ? token[..6] : token;

    static (float[] Values, uint[] Indices) Build(IEnumerable<(string Token, float Weight)> terms) {
        // Разные слова могут дать одинаковый хеш — складываем веса (коллизии 32-битного хеша редки).
        var map = new Dictionary<uint, float>();
        foreach (var (token, weight) in terms) {
            var index = Fnv1a(token);
            map[index] = map.GetValueOrDefault(index) + weight;
        }
        var ordered = map.OrderBy(kv => kv.Key).ToArray();
        return (ordered.Select(kv => kv.Value).ToArray(), ordered.Select(kv => kv.Key).ToArray());
    }

    // Стабильный хеш: string.GetHashCode в .NET меняется между запусками и для индекса не годится.
    static uint Fnv1a(string s) {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(s)) {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[.\-_][\p{L}\p{N}]+)*")]
    private static partial Regex TokenRegex();

    static readonly HashSet<string> StopWords =
    [
        "и", "в", "во", "не", "что", "он", "на", "я", "с", "со", "как", "а", "то", "все", "она", "так", "его", "но",
        "да", "ты", "к", "у", "же", "вы", "за", "бы", "по", "только", "ее", "мне", "было", "вот", "от", "меня", "еще",
        "нет", "о", "из", "ему", "теперь", "когда", "уже", "вам", "ни", "быть", "был", "до", "или", "если", "ли",
        "есть", "для", "мы", "при", "это", "этот", "эта", "эти", "там", "где", "кто", "чем", "их", "можно", "нужно",
        "какой", "какая", "какие", "сколько", "почему", "зачем", "мой", "моя", "мои", "свой", "через", "после", "без",
    ];
}