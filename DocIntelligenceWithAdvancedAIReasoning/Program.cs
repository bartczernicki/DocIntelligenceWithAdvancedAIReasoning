using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using DocIntelligenceSemanticChunking.Objects;
using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.ML.Tokenizers;

namespace DocIntelligenceSemanticChunking
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Used to track Time
            var startTime = DateTime.UtcNow;

            // Build Configuration
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.configuration.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>();
            var configuration = configurationBuilder.Build();

            // Workflow Settings
            var skipDocumentIntelligence = Convert.ToBoolean(configuration["skipDocumentIntelligence"]);
            var skipDynamicDocumentGroupings = Convert.ToBoolean(configuration["skipDynamicDocumentGroupings"]);
            var skipMarkdownToJsonConversion = Convert.ToBoolean(configuration["skipMarkdownToJsonConversion"]);
            var skipIntermediateRiskFactors = Convert.ToBoolean(configuration["skipIntermediateRiskFactors"]);
            var skipConsolidatedRiskAnalysis = Convert.ToBoolean(configuration["skipConsolidatedRiskAnalysis"]);
            // Document Locations
            var document1 = configuration["document1"]!;
            var document2 = configuration["document2"]!;

            Console.WriteLine("PERFORMING DOCUMENT COMPARISON ANALYSIS");
            Console.WriteLine("---------------------------------------");

            // 0 - CONFIGURATION 

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 0 - CONFIGURATION...");
            Console.ResetColor();

            // Azure OpenAI Configuration from user secrets
            //ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            // IConfiguration configuration = configurationBuilder.AddUserSecrets<Program>().Build();

            // Retrieve the Azure OpenAI Configuration Section (secrets.json)
            var azureOpenAISection = configuration.GetSection("AzureOpenAI");
            var o1AzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["o1Endpoint"];
            var o1AzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["o1ModelDeploymentName"];
            var o1AzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["o1APIKey"];
            var gpt4oAzureOpenAIEndpoint = configuration.GetSection("AzureOpenAI")["gpt4oEndpoint"];
            var gpt4oAzureModelDeploymentName = configuration.GetSection("AzureOpenAI")["gpt4oModelDeploymentName"];
            var gpt4oAzureOpenAIAPIKey = configuration.GetSection("AzureOpenAI")["gpt4oAPIKey"];

            // The output directory for the o1 model markdown analysis files
            var o1OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");

            var retryPolicy = new ClientRetryPolicy(maxRetries: 3);
            AzureOpenAIClientOptions azureOpenAIClientOptions = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21);
            azureOpenAIClientOptions.RetryPolicy = retryPolicy;
            azureOpenAIClientOptions.NetworkTimeout = TimeSpan.FromMinutes(10); // Large Timeout

            Uri azureOpenAIResourceUri = new(o1AzureOpenAIEndpoint!);
            var azureApiCredential = new System.ClientModel.ApiKeyCredential(o1AzureOpenAIAPIKey!);

            var o1Client = new AzureOpenAIClient(azureOpenAIResourceUri, azureApiCredential, azureOpenAIClientOptions);
            var gpt4oClient = new AzureOpenAIClient(new Uri(gpt4oAzureOpenAIEndpoint!), new System.ClientModel.ApiKeyCredential(gpt4oAzureOpenAIAPIKey!), azureOpenAIClientOptions);

            var completionOptions = new ChatCompletionOptions()
            {
                // Temperature = 1f,
                EndUserId = "o1Analysis",
                //MaxOutputTokenCount = 10000
                // TopLogProbabilityCount = true ? 5 : 1 // Azure OpenAI maximum is 5               
            };

            var gpt4oCompletionOptions = new ChatCompletionOptions()
            {
                Temperature = 0.3f,
                EndUserId = "GPtoAnalysis",
                MaxOutputTokenCount = 10000
                // TopLogProbabilityCount = true ? 5 : 1 // Azure OpenAI maximum is 5               
            };

            // Set up Azure Document Intelligence client
            var endpoint = configuration.GetSection("DocumentIntelligence")["Endpoint"];
            var key = configuration.GetSection("DocumentIntelligence")["APIKey"];
            AzureKeyCredential credential = new AzureKeyCredential(key!);
            DocumentIntelligenceClient client = new DocumentIntelligenceClient(new Uri(endpoint!), credential);

            // Get the file name without extension
            var document1Name = Path.GetFileName(document1);
            var document2Name = Path.GetFileName(document2);
            var documents = new List<string> { document1, document2 };
            var documentStructures = new List<DocumentStructure>();

            // 1 - DOCUMENT INTELLIGENCE 
            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 1 - DOCUMENT INTELLIGENCE: ANALYING DOCUMENT STRUCTURE...");
            Console.ResetColor();

            // Skip Document Intelligence if needed, and use a local file
            if (!skipDocumentIntelligence)
            {
                ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };
                var cancellationToken = new CancellationTokenSource();
                await Parallel.ForEachAsync(documents, parallelOptions, async (document, cancellationToken) =>
                {
                    Console.WriteLine($"Converting Document to Binary...{document}");
                    AnalyzeDocumentContent content = new AnalyzeDocumentContent()
                    {
                        Base64Source = BinaryData.FromBytes(File.ReadAllBytes(document))
                    };

                    Console.WriteLine($"Cracking Document...{document}");

                    //var analysisFeatures = new List<DocumentAnalysisFeature>();
                    //analysisFeatures.Add(DocumentAnalysisFeature.StyleFont);

                    Operation<AnalyzeResult> operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", content, outputContentFormat: "markdown");

                    Console.WriteLine($"Parsing Markdown...{document}");
                    AnalyzeResult result = operation.Value;

                    // Parse Markdown using Semantic Kernel
                    var markDownContentString = result.Content;

                    // Write local file
                    DirectoryInfo di = Directory.CreateDirectory(o1OutputDirectory);
                    var documentName = Path.GetFileNameWithoutExtension(document);
                    var markdownFilePath = Path.Combine(o1OutputDirectory, $"{documentName!.ToUpper()}.MD");
                    File.WriteAllText(markdownFilePath, markDownContentString);
                }); // end of Document Intelligence - Parallel.ForEachAsync over documents
            }
            else
            {
                Console.WriteLine("Skipping Document Intelligence...");
            }


            // 2 - MARKDOWN PROCESSING 
            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 2 - MARKDOWN PROCESSING...");
            Console.ResetColor();

            foreach (var document in documents)
            {
                // Markdown Processing
                // Read local file
                var markdownDocument = Path.GetFileNameWithoutExtension(document);
                var markdownDocumentFilePath = Path.Combine(o1OutputDirectory, $"{markdownDocument!.ToUpper()}.MD");
                var markDownContentString = File.ReadAllText(markdownDocumentFilePath);

                var builder = new MarkdownPipelineBuilder()
                    .UseGridTables()
                    .EnableTrackTrivia()
                    .UsePreciseSourceLocation();
                var pipeline = builder.Build();

                // Parse Markdown using Markdown library
                var parsedMarkdown = Markdown.Parse(markDownContentString, pipeline);

                var headingsMap = new List<Heading>();
                // Print the parsed markdown
                foreach (var item in parsedMarkdown.Descendants())
                {
                    // Get the type of the item
                    var type = item.GetType();
                    // Console.WriteLine("Type: " + type);

                    // Iterate over all of the headings, after Table of Contents
                    var headingIndex = 0;
                    if (item is HeadingBlock headingItem)
                    {
                        var markdownName = markDownContentString.Substring(headingItem.Span.Start, headingItem.Span.Length);
                        // Remove # character from the heading name
                        var headingName = markdownName.Replace("#", string.Empty).Trim();

                        // If Previous Heading Exists, Set the Text
                        var previousHeading = headingsMap.LastOrDefault();
                        if (previousHeading != null)
                        {
                            var textLength = headingItem.Span.Start - previousHeading.NameEnd - 1;
                            previousHeading.Text =
                                markDownContentString.Substring(previousHeading.NameEnd + 1, textLength);
                        }

                        headingsMap.Add(new Heading
                        {
                            MarkdownName = markdownName,
                            Name = headingName,
                            NameStart = headingItem.Span.Start,
                            NameEnd = headingItem.Span.End,
                            Index = headingIndex,
                            Text = markDownContentString.Substring(headingItem.Span.End + 1, markDownContentString.Length - (headingItem.Span.End + 1))
                        });
                        // Console.WriteLine("Heading: " + headingName);
                    };

                    //if (item is ParagraphBlock paragraphItem)
                    //{
                    //    var subTest = markDownContentString.Substring(paragraphItem.Span.Start, paragraphItem.Span.Length);
                    //    Console.WriteLine("Paragraph: " + subTest);
                    //};

                    //if (item is LiteralInline literalItem)
                    //{
                    //    var subTest = markDownContentString.Substring(literalItem.Span.Start, literalItem.Span.Length);
                    //    Console.WriteLine("Literal: " + subTest);
                    //};

                    //if (item is HtmlBlock htmlBlock)
                    //{
                    //    var subTest = markDownContentString.Substring(htmlBlock.Span.Start, htmlBlock.Span.Length);
                    //    Console.WriteLine("HTML: " + subTest);
                    //};
                } // EOF Markdown Parse ForEach

                documentStructures.Add(new DocumentStructure 
                    { 
                        DocumentName = markdownDocument,
                        HeadingsMap = headingsMap
                    }
                );

                Console.WriteLine($"{markdownDocument} Headings Count: {headingsMap.Count()}");
            }


            // 3 - ANALYSIS OF TABLE OF CONTENTS 
            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 3 - GENAI: DYNAMIC DOCUMENT GROUPINGS...");
            Console.ResetColor();

            if (!skipDynamicDocumentGroupings)
            {
                Console.WriteLine("First Pass...");
                // Retrieve the Prompt
                var tableOfContentsPrompt = Prompts.GetFullPromptForTableOfContentsConsolidation(documentStructures);

                // Build the Chat Message & Execute the Prompt
                var chatMessagesTableOfContents = new List<ChatMessage>();
                chatMessagesTableOfContents.Add(tableOfContentsPrompt);

                var chatClient = o1Client.GetChatClient(o1AzureModelDeploymentName);

                var tableOfContentsPromptResponse = await chatClient.CompleteChatAsync(chatMessagesTableOfContents, completionOptions);
                var tableOfContentsPromptResponseOutputDetails = tableOfContentsPromptResponse.Value.Usage.OutputTokenDetails;
                var tableOfContentsPromptResponseTotalTokenCount = tableOfContentsPromptResponse.Value.Usage.TotalTokenCount;

                var tableOfContentsPath = Path.Combine(o1OutputDirectory, $"TABLEOFCONTENTSGROUPING.MD");
                var tableOfContentsPromptResponseText = tableOfContentsPromptResponse.Value.Content.FirstOrDefault()!.Text;
                File.WriteAllText(tableOfContentsPath, tableOfContentsPromptResponseText);


                // Second Pass
                Console.WriteLine("Second Pass...");
                var tableOfContentsPromptSecond = Prompts.GetFullPromptForTableOfContentsConsolidationSecondPass(documentStructures, tableOfContentsPromptResponseText);

                // Build the Chat Message & Execute the Prompt
                var chatMessagesTableOfContentsSecond = new List<ChatMessage>();
                chatMessagesTableOfContentsSecond.Add(tableOfContentsPromptSecond);

                var chatClientSecond = o1Client.GetChatClient(o1AzureModelDeploymentName);

                var tableOfContentsPromptResponseSecond = await chatClientSecond.CompleteChatAsync(chatMessagesTableOfContentsSecond, completionOptions);

                var tableOfContentsPathFinal = Path.Combine(o1OutputDirectory, $"TABLEOFCONTENTSGROUPINGFINAL.MD");
                var tableOfContentsPromptResponseSecondText = tableOfContentsPromptResponseSecond.Value.Content.FirstOrDefault()!.Text;
                File.WriteAllText(tableOfContentsPathFinal, tableOfContentsPromptResponseSecondText);
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 4 - GENAI: CREATE JSON STRUCTURE...");
            Console.ResetColor();

            if (!skipMarkdownToJsonConversion)
            {
                var tableOfContentsMarkdownPathFinal = Path.Combine(o1OutputDirectory, $"TABLEOFCONTENTSGROUPINGFINAL.MD");
                var tableOfContentsMarkdownFinal = File.ReadAllText(tableOfContentsMarkdownPathFinal);

                var chatClientGPT4o = gpt4oClient.GetChatClient(gpt4oAzureModelDeploymentName);
                var chatMessagesGPT4o = new List<ChatMessage>();
                var markdownToJsonPrompt = Prompts.GetFullPromptForMarkdownToJsonConversion(tableOfContentsMarkdownFinal);
                chatMessagesGPT4o.Add(markdownToJsonPrompt);

                var responseGPT4o = await chatClientGPT4o.CompleteChatAsync(chatMessagesGPT4o, gpt4oCompletionOptions);
                var convertedJson = responseGPT4o.Value.Content.FirstOrDefault()!.Text;

                var jsonFilePath = Path.Combine(o1OutputDirectory, $"TABLEOFCONTENTSGROUPINGFINAL.JSON");
                File.WriteAllText(jsonFilePath, convertedJson);

                Console.WriteLine("Created JSON structure file.");
            }
            else
            {
                Console.WriteLine("Skipping Markdown to JSON Conversion...");
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 5 - GENAI: CREATE INTERMEDIATE RISK FACTORS...");
            Console.ResetColor();

            if (!skipIntermediateRiskFactors)
            {
                var jsonStructureFilePath = Path.Combine(o1OutputDirectory, $"TABLEOFCONTENTSGROUPINGFINAL.JSON");
                var jsonStructure = File.ReadAllText(jsonStructureFilePath);
                // convert to object
                var riskFactorGroupings = JsonSerializer.Deserialize<List<RiskFactorGroupings>>(jsonStructure);

                var riskAnalysisIntermediate = Path.Combine(Directory.GetCurrentDirectory(), "Output", "RiskAnalysisIntermediate");
                DirectoryInfo di = Directory.CreateDirectory(riskAnalysisIntermediate);

                ParallelOptions parallelOptionsRisk = new ParallelOptions { MaxDegreeOfParallelism = 6 };
                //var cancellationToken = new CancellationTokenSource();
                await Parallel.ForEachAsync(riskFactorGroupings!, parallelOptionsRisk, async (sectionGrouping, cancellationToken) =>
                {
                    var groupTexts1String = Data.GetSectionGroupingsWithTextParagraphs(sectionGrouping.Document1Sections, documentStructures[0].HeadingsMap);
                    var groupTexts2String = Data.GetSectionGroupingsWithTextParagraphs(sectionGrouping.Document2Sections, documentStructures[1].HeadingsMap);

                    // This prompt will summarize each grouping and provide a comparison between the two documents groupings
                    var riskGroupingAnalysisPrompt = Prompts.GetFullPromptForSectionGroupRiskAnalysis(sectionGrouping.GroupName, groupTexts1String, groupTexts2String);
                    // Calculate the Prompt Tokens for Section
                    Tokenizer sectionTokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
                    var sectionPromptTokenCount = sectionTokenizer.CountTokens(riskGroupingAnalysisPrompt);
                    Console.WriteLine($"{sectionGrouping.GroupName} - Prompt Token Count: {sectionPromptTokenCount}");

                    // Build the Chat Message & Execute the Prompt
                    var chatMessagesRiskGroupingAnalysis = new List<ChatMessage>();
                    chatMessagesRiskGroupingAnalysis.Add(riskGroupingAnalysisPrompt);

                    var chatClientSecond = o1Client.GetChatClient(o1AzureModelDeploymentName);
                    var riskGroupingAnalysisPromptResponse = await chatClientSecond.CompleteChatAsync(chatMessagesRiskGroupingAnalysis, completionOptions);

                    var groupNameFixed = sectionGrouping.GroupName.Replace("/", string.Empty);
                    var riskGroupingAnalysisTable = Path.Combine(riskAnalysisIntermediate, $"{groupNameFixed.ToUpper()}.MD");
                    var riskGroupingAnalysisPromptResponseText = riskGroupingAnalysisPromptResponse.Value.Content.FirstOrDefault()!.Text;
                    File.WriteAllText(riskGroupingAnalysisTable, riskGroupingAnalysisPromptResponseText);
                }); // end of Risk Factor Groupings - Parallel.ForEachAsync over riskFactorGroupings
            } // EOF skipIntermediateRiskFactors
            else
            {
                Console.WriteLine("Skipping Intermediate Risk Factors...");
            }

            Console.WriteLine(string.Empty);
            Console.WriteLine(string.Empty);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"STEP 6 - GENAI: CONSOLIDATE RISK FACTORS...");
            Console.ResetColor();

            if (!skipConsolidatedRiskAnalysis)
            {
                // Read each file and extract the table
                var riskAnalysisIntermediateDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output", "RiskAnalysisIntermediate");
                var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Output");

                var markdownFiles = Directory.GetFiles(riskAnalysisIntermediateDirectory, "*.MD");

                var markdownTables = new List<string>();
                foreach (var file in markdownFiles)
                {
                    var markdown = File.ReadAllText(file);
                    markdownTables.Add(markdown);
                }

                var riskConsolidationPrompt = Prompts.GetFullPromptToConsolidateImportantRisks(markdownTables);
                var riskConsolidationPromptUserMessage = new UserChatMessage(riskConsolidationPrompt);
                var riskConsolidationPromptChatMessages = new List<ChatMessage>();
                riskConsolidationPromptChatMessages.Add(riskConsolidationPromptUserMessage);

                Console.WriteLine("Consolidating Markdown...");
                Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
                var tokenCount = tokenizer.CountTokens(riskConsolidationPrompt);
                Console.WriteLine($"Risk Consolidation Prompt Token Count: {tokenCount}");

                var chatCliento1 = o1Client.GetChatClient(o1AzureModelDeploymentName);
                var riskConsolidationPromptResponse = await chatCliento1.CompleteChatAsync(riskConsolidationPromptChatMessages, completionOptions);

                var riskConsolidationTable = Path.Combine(outputDirectory, $"CONSOLIDATEDRISKANALYSIS.MD");
                var riskConsolidationPromptResponseText = riskConsolidationPromptResponse.Value.Content.FirstOrDefault()!.Text;
                File.WriteAllText(riskConsolidationTable, riskConsolidationPromptResponseText);
            }
            {
                Console.WriteLine("Skipping Consolidate Risk Analysis...");
            }

            // Report finish Time
            var endTime = DateTime.UtcNow;
            var durationSections = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Total Duration: {durationSections} seconds");

        } // EOF Main 
    } // EOF Program
} // EOF Namespace
