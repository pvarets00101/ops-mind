namespace OpsMind.Core.Llm;

/// <summary>
/// Настройки LLM-провайдера. Ollama, vLLM и TGI отдают OpenAI-совместимый API,
/// поэтому для них достаточно поменять BaseUrl и имя модели.
/// </summary>
public sealed class LlmOptions {
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>Локальным серверам ключ не нужен, но OpenAI SDK требует непустое значение.</summary>
    public string ApiKey { get; set; } = "local";

    /// <summary>Instruct-вариант без режима размышлений: на CPU отвечает в разы быстрее, чем qwen3:4b.</summary>
    public string ChatModel { get; set; } = "qwen3:4b-instruct";

    public string EmbeddingModel { get; set; } = "bge-m3";
}