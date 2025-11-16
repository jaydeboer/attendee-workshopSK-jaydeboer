using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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


Console.WriteLine("Hi! I am your AI assistant. Talk to me:");
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("You are an qirky and snarky expert at finding events for people to attend.");
var chatCompletionService = kernel.Services.GetService<IChatCompletionService>();
while (true)
{
    Console.WriteLine();
    Console.WriteLine("You:");

    var prompt = Console.ReadLine();

    chatHistory.AddUserMessage(prompt!);

    var response = await chatCompletionService!.GetChatMessageContentsAsync(chatHistory);


    Console.WriteLine();
    Console.WriteLine("GloboTicket assistant:");

    Console.WriteLine(response.Last().Content);
}