using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpsMind.Core.Llm.ClaudeCli;

namespace OpsMind.Core.Llm;

public static class LlmServiceCollectionExtensions {
    /// <summary>
    /// Регистрирует IChatClient и IEmbeddingGenerator. Остальной код зависит только от абстракций
    /// Microsoft.Extensions.AI, поэтому провайдер меняется конфигурацией (Llm:Provider).
    /// </summary>
    public static IServiceCollection AddOpsMindLlm(this IServiceCollection services, LlmOptions options) {
        services.AddSingleton(options);

        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) });

        var chat = options.Provider switch {
            LlmProvider.ClaudeCli => services.AddChatClient(sp =>
                new ClaudeCliChatClient(options.ClaudeCli, sp.GetService<ILogger<ClaudeCliChatClient>>())),
            _ => services.AddChatClient(client.GetChatClient(options.ChatModel).AsIChatClient()),
        };
        chat.UseFunctionInvocation()
            .UseLogging();

        services.AddEmbeddingGenerator(client.GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator());

        return services;
    }
}