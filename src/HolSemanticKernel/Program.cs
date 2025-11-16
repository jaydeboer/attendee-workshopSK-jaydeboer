using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
//using Microsoft.SemanticKernel.ChatCompletions;

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

// var client = new ChatCompletionsClient(
//     new Uri(endpoint),
//     new AzureKeyCredential(token),
//     new AzureAIInferenceClientOptions());

// var requestOptions = new ChatCompletionsOptions()
// {
//     Model = model,
//     Temperature = 0.99f,

//     Messages =
//     [
//         new ChatRequestSystemMessage("You are a world-famous comedian that likes to pick on javascript."),
//         new ChatRequestUserMessage("Tell me a joke about programming or programmers.")
//     ]
// };

// var resp = await client.CompleteAsync(requestOptions);
// Console.WriteLine(resp.Value.Content);
var kernelBuilder = Kernel
    .CreateBuilder()
    .AddOpenAIChatCompletion(model, new Uri(endpoint), token);

var kernel = kernelBuilder.Build();
var executionSettings = new OpenAIPromptExecutionSettings
{
    MaxTokens = 500,
    Temperature = 0.5,
    TopP = 1.0,
    FrequencyPenalty = 0.0,
    PresencePenalty = 0.0
};

Console.WriteLine("Hi! I am your AI assistant. Talk to me:");
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("You are a digital assistant for GloboTicket, a concert ticketing company. You help customers with their ticket purchasing. Tone: warm and friendly, but to the point.  Do not make things up when you don't know the answer. Just tell the user that you don't know the answer based on your knowledge.");
var chatCompletionService = kernel.Services.GetService<IChatCompletionService>();
while (true)
{
    Console.WriteLine();
    Console.WriteLine("You:");

    var prompt = Console.ReadLine();

    Console.WriteLine();
    Console.WriteLine("GloboTicket assistant:");

    chatHistory.AddUserMessage(prompt!);

    // var response = await chatCompletionService!.GetChatMessageContentsAsync(chatHistory);
    var responseStream = chatCompletionService!.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings);
    await foreach (var response in responseStream)
    {
        Console.Write(response.Content);
    }

    // Console.WriteLine(response.Last().Content);
}