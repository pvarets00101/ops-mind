using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace OpsMind.Core.Agent;

public static class AgentServiceCollectionExtensions
{
    /// <summary>Регистрирует агента и его инструменты. Требует AddOpsMindLlm и AddOpsMindRag.</summary>
    public static IServiceCollection AddOpsMindAgent(this IServiceCollection services, AgentOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IncidentDatabase>();
        services.AddSingleton<AttachmentStore>();
        // Пул соединений приложения (роль opsmind_app): диалоги и ожидающие действия.
        // SQL-инструмент агента ходит отдельно, под read-only ролью (IncidentDatabase).
        services.AddSingleton(_ => NpgsqlDataSource.Create(options.AppConnectionString));
        services.AddSingleton<PendingActionStore>();
        services.AddSingleton<ConversationStore>();
        services.AddSingleton<PostgresChatHistoryProvider>();
        services.AddSingleton<OpsMindTools>();
        services.AddSingleton<OpsMindAgent>();

        // Повторные попытки, таймауты и circuit breaker для ITSM (Polly через Microsoft.Extensions.Http.Resilience).
        services.AddHttpClient<TicketClient>(c => c.BaseAddress = new Uri(options.TicketServiceUrl))
            .AddStandardResilienceHandler(r =>
            {
                r.Retry.MaxRetryAttempts = 4;
                r.Retry.Delay = TimeSpan.FromMilliseconds(200); // экспоненциально: 0.2, 0.4, 0.8, 1.6 с + jitter
            });

        return services;
    }
}
