using System;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace modulerag;

public class ChatWithRag
{
    public async Task RAG_with_single_prompt(Kernel kernel)
    {
        var question = 
            """
            I booked tickets for a concert tonight in venue AFAS Live!.
            I have this small black backpack, not big like for school, more like the mini
            festival type 😅. it just fits my wallet, a hoodie and a bottle of water.
            Is this allowed?
            """;

            var venue = await GetVenueFromQuestion(kernel, question);
            var policyContext = await GetVenuePolicyFileContents(kernel, venue);
            await GetResponseOnQuestion(kernel, question, policyContext);

    }

    private async Task GetResponseOnQuestion(Kernel kernel, string question, string policyContext)
    {
        // Path is relative to the bin/Debug/net9.0 folder where your application runs
        // var policyContext = File.ReadAllText("../../../../datasets/venue-policies/AFAS_Live.md");

        ChatHistory chatHistory = new();
        chatHistory.AddSystemMessage("You are a helpful assistant that answers questions from people that go to a concert and have questions about the venue.");
        chatHistory.AddSystemMessage("Always use the policy information provided in the prompt");
        chatHistory.AddSystemMessage($"### Venue Policy\n {policyContext}");

        chatHistory.AddUserMessage(question);
        
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var questionResponse = chatCompletionService!.GetStreamingChatMessageContentsAsync(chatHistory, kernel:kernel);
        await foreach (var response in questionResponse)
        {
            Console.Write(response.Content);
        }
    }
    
    private async Task<string> GetVenueFromQuestion(Kernel kernel, string question)
    {
        ChatHistory chatHistory = new();

        chatHistory.AddSystemMessage("You are a helpful asistant that finds the name of a venue from a question.");
        chatHistory.AddSystemMessage("Always get the information from the question. Never search the web or use internal knowledge!");
        chatHistory.AddUserMessage(question);
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(SelectedVenue)
        };
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        var selectedVenue = JsonSerializer.Deserialize<SelectedVenue>(result.ToString());
        return selectedVenue!.VenueName;
    }

    private async Task<string> GetVenuePolicyFileContents(Kernel kernel, string venueName)
    {
        //Get a list of files from the venue policy repository
        var directory = "../../../../datasets/venue-policies";
        var fileList = string.Join("\n", System.IO.Directory.GetFiles(directory, "*.md").Select(f => System.IO.Path.GetFileName(f)));
        
        var systemPrompt = "You are an expert at finding the correct file based on a user question.";
        var fileListPrompt = $"The following is a list of files available:\n{fileList}";
        var fileQuestion = $"Which file contains the venue policy for the venue named '{venueName}'?";
        
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(fileListPrompt);
        chatHistory.AddUserMessage(fileQuestion);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(SelectedFile)
        };

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        
        var fileResult = JsonSerializer.Deserialize<SelectedFile>(result.ToString());
        var fullFileName = Path.Combine(directory, fileResult.File);

        if (System.IO.File.Exists(fullFileName))
        {
            using (var file =  File.OpenText(fullFileName))
            {
                return await file.ReadToEndAsync();
            }
        }
        
        return "No Policy information found";
    }

    public async Task IngestDocuments(IConfiguration config)
    {
        var directory = "../../../../datasets/venue-policies";

        var memoryConnector = GetLocalKernelMemory(config);

        foreach (var file in GetFileListOfPolicyDocuments(directory))
        {
            var fullfilename = Path.Combine(directory, file);
            var importResult = await memoryConnector.ImportDocumentAsync(filePath: fullfilename, documentId: file);
            Console.WriteLine($"Imported file {file} with result: {importResult}");
        }
    }

    private IKernelMemory GetLocalKernelMemory(IConfiguration config)
    {
        // 1. Configure text generation service
        var textGenerationConfig = new OpenAIConfig
        {
            Endpoint = config["OpenAI:EndPoint"]!,
            APIKey = config["OpenAI:ApiKey"]!,
            TextModel = config["OpenAI:Model"]!,
        };

        // 2. Configure embedding generation service
        var openAiEmbeddingsConfig = new OpenAIConfig
        {
            APIKey = config["OpenAI:ApiKey"]!,
            Endpoint = config["OpenAI:EndPoint"]!,
            EmbeddingModel = config["OpenAI:EmbeddingModel"]!,
        };

        var kernelMemoryBuilder = new KernelMemoryBuilder()
        // 3. Configure file storage backend
        .WithSimpleFileStorage(new SimpleFileStorageConfig
        {
            Directory = "kernel-memory/km-file-storage",
            StorageType = FileSystemTypes.Disk
        })
        // 4. Configure text database backend
        .WithSimpleTextDb(new SimpleTextDbConfig
        {
            Directory = "kernel-memory/km-text-db",
            StorageType = FileSystemTypes.Disk
        })
        // 5. Configure vector database backend
        .WithSimpleVectorDb(new SimpleVectorDbConfig
        {
            Directory = "kernel-memory/km-vector-db",
            StorageType = FileSystemTypes.Disk
        })
        // 6. Integrate AI services
        .WithOpenAITextEmbeddingGeneration(openAiEmbeddingsConfig)
        .WithOpenAITextGeneration(textGenerationConfig);

        return kernelMemoryBuilder.Build();
    }

    private IEnumerable<string> GetFileListOfPolicyDocuments(string directory)
    {
        return Directory.GetFiles(directory, "*.pdf").Select(f => Path.GetFileName(f));
    }
}
