using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpsMind.Core.Llm;

namespace OpsMind.Tests;

public class LlmRegistrationTests
{
    [Fact]
    public void AddOpsMindLlm_RegistersChatAndEmbeddingClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpsMindLlm(new LlmOptions());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IChatClient>());
        Assert.NotNull(provider.GetService<IEmbeddingGenerator<string, Embedding<float>>>());
    }
}
