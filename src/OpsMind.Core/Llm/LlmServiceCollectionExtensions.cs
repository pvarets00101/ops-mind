using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace OpsMind.Core.Llm;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует IChatClient и IEmbeddingGenerator поверх OpenAI-совместимого endpoint.
    /// Остальной код зависит только от абстракций Microsoft.Extensions.AI,
    /// поэтому провайдер меняется конфигурацией.
    /// </summary>
    public static IServiceCollection AddOpsMindLlm(this IServiceCollection services, LlmOptions options)
    {
        services.AddSingleton(options);

        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) });

        services.AddChatClient(client.GetChatClient(options.ChatModel).AsIChatClient())
            .UseFunctionInvocation()
            .UseLogging();

        services.AddEmbeddingGenerator(client.GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator());

        return services;
    }
}
