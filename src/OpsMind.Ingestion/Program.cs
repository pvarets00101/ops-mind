using OpsMind.Core.Agent;
using OpsMind.Core.Llm;
using OpsMind.Core.Rag;
using OpsMind.Ingestion;

// Запуск из корня репозитория: dotnet run --project src/OpsMind.Ingestion
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpsMindLlm(builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions());
builder.Services.AddOpsMindRag(builder.Configuration.GetSection(RagOptions.SectionName).Get<RagOptions>() ?? new RagOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions());
builder.Services.AddSingleton<AttachmentStore>();
builder.Services.AddHostedService<IndexingWorker>();

builder.Build().Run();