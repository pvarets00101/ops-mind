using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;

namespace OpsMind.Core.Rag;

public static class RagServiceCollectionExtensions
{
    /// <summary>Регистрирует Qdrant, базу знаний и RAG. Требует AddOpsMindLlm (нужен IEmbeddingGenerator и IChatClient).</summary>
    public static IServiceCollection AddOpsMindRag(this IServiceCollection services, RagOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(_ => new QdrantClient(options.QdrantHost, options.QdrantPort));
        services.AddSingleton(_ => new Reranker(new HttpClient
        {
            BaseAddress = new Uri(options.RerankerUrl ?? "http://localhost:8081/"),
            Timeout = TimeSpan.FromSeconds(60), // на CPU 20 пар переоцениваются несколько секунд
        }));
        services.AddSingleton<KnowledgeBase>();
        services.AddSingleton<RagAnswerer>();
        return services;
    }
}
