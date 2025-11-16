using HolSemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using ModelContextProtocol.Client;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var model = config["OpenAI:Model"];
var endpoint = config["OpenAI:EndPoint"];
var token = config["OpenAI:ApiKey"];

Console.WriteLine($"Model: {model}");
Console.WriteLine($"Endpoint: {endpoint}");

if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(token))
{
    throw new ArgumentException("Missing configuration");
}

var kernelBuilder = Kernel
    .CreateBuilder()
    .AddOpenAIChatCompletion(model, new Uri(endpoint), token);

kernelBuilder.Services.AddTransient<IFunctionInvocationFilter, AnonymousUserFilter>();

var kernel = kernelBuilder.Build();

var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(
 new HttpClientTransportOptions
 {
     Name = "GitHub",
     Endpoint = new Uri("https://api.githubcopilot.com/mcp/"),
     AdditionalHeaders = new Dictionary<string, string>
     {
         ["Authorization"] = $"Bearer {config["GitHubToken"]}"
     }
 }));
var promptTemplate = File.ReadAllText(Path.Join(Directory.GetCurrentDirectory(), "MusicRecommender.yaml"));

var musicRecommender = kernel.CreateFunctionFromPromptYaml(
  promptTemplate,
  new HandlebarsPromptTemplateFactory()
  {
      AllowDangerouslySetContent = true
  });

kernel.ImportPluginFromFunctions("music_recommender", [musicRecommender]);kernel.ImportPluginFromType<Microsoft.SemanticKernel.Plugins.Core.TimePlugin>();
kernel.ImportPluginFromType<DiscountPlugin>();

var tools = await mcpClient.ListToolsAsync();

kernel.ImportPluginFromFunctions(
  "GitHub",
  tools.Select(x => x.AsKernelFunction()));

var executionSettings = new OpenAIPromptExecutionSettings
{
    MaxTokens = 500,
    Temperature = 0.5,
    TopP = 1.0,
    FrequencyPenalty = 0.0,
    PresencePenalty = 0.0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

Console.WriteLine("Hi! I am your AI assistant. Talk to me:");
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("You are a digital assistant for GloboTicket, a concert ticketing company. You help customers with their ticket purchasing. Tone: warm and friendly, but to the point.  Do not make things up when you don't know the answer. Just tell the user that you don't know the answer based on your knowledge.");
var chatCompletionService = kernel.Services.GetService<IChatCompletionService>();

var multiModalChat = new ChatHistory("Your job is to identify musical instruments from images.");
multiModalChat.AddUserMessage(
[
  new Microsoft.SemanticKernel.TextContent("Can you identify this instrument? Be specific about brand and type."),
  new Microsoft.SemanticKernel.ImageContent(File.ReadAllBytes("guitar.jpg"), "image/jpg")
]);

// var multiModalChat = new ChatHistory("Your job is to answer questions base on the provided document.");
// #pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
// multiModalChat.AddUserMessage(
// [
//     new Microsoft.SemanticKernel.TextContent("What pin is used for the timer T0CKI"),
//     new BinaryContent(File.ReadAllBytes("PIC16F15213-14-23-24-43-44-Data-Sheet-40002195D.pdf"), "application/pdfgh auth login")
// ]);
// #pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var multiModalResponse = await chatCompletionService!.GetChatMessageContentsAsync(multiModalChat, executionSettings, kernel);
Console.WriteLine(multiModalResponse.Last().Content);
while (true)
{
    Console.WriteLine();
    Console.WriteLine("You:");

    var prompt = Console.ReadLine();

    Console.WriteLine();
    Console.WriteLine("GloboTicket assistant:");

    chatHistory.AddUserMessage(prompt!);

    var responseStream = chatCompletionService!.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel);
    await foreach (var response in responseStream)
    {
        Console.Write(response.Content);
    }
}