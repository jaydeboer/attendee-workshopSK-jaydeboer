using Microsoft.Extensions.Configuration;
using modulerag;

var builder = new ConfigurationBuilder();
builder.SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>();

IConfiguration config = builder.Build();

await new ChatWithRag().IngestDocuments(config);
//await new ChatWithRag().AskVenueQuestion(config);
//await new ChatWithRag().RAG_with_memory(config);
