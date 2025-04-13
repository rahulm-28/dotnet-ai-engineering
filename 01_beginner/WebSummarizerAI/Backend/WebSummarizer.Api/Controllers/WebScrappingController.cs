using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace WebSummarizer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebScrappingController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public WebScrappingController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
        }

        [HttpGet("webscrape")]
        public async Task<IActionResult> HandleIncomingRequests([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("Please provide a valid URL to scrape.");

            var content = await ScrapeWeb(url);
            Console.WriteLine("SCRAPED CONTENT:");
            Console.WriteLine(content);
            return await ConnectToAzureOpenAI(content);
        }


        private async Task<IActionResult> ConnectToAzureOpenAI(string content)
        {
            var endpoint = _configuration["AZURE_OPENAI_ENDPOINT"];
            if (string.IsNullOrEmpty(endpoint))
            {
                Console.WriteLine("Please set the AZURE_OPENAI_ENDPOINT environment variable.");
                return NotFound();
            }

            var key = _configuration["AZURE_OPENAI_KEY"];
            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("Please set the AZURE_OPENAI_KEY environment variable.");
                return NotFound();
            }

            AzureKeyCredential credential = new AzureKeyCredential(key);

            // Initialize the AzureOpenAIClient
            AzureOpenAIClient azureClient = new(new Uri(endpoint), credential);

            // Initialize the ChatClient with the specified deployment name
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini (version:2024-07-18)");

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an AI assistant that summarizes webpages. Extract the key points and present them in a concise, easy-to-read summary."),

                new UserChatMessage($"Summarize the following webpage content:\n\n{content}")

            };


            // Create chat completion options

            var options = new ChatCompletionOptions
            {
                Temperature = (float)0.7,
                MaxOutputTokenCount = 800,

                TopP = (float)0.95,
                FrequencyPenalty = (float)0,
                PresencePenalty = (float)0
            };

            try
            {
                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

                if (completion != null)
                {
                    return Ok(JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true }));
                }
                else
                {
                    Console.WriteLine("No response received.");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            return Ok();
        }

        private async Task<string> ScrapeWeb(string urli)
        {
            try
            {
                var response = await _httpClient.GetAsync(urli);

                if (!response.IsSuccessStatusCode)
                {
                    return "Website blocked for scraping.";
                }

                var html = await response.Content.ReadAsStringAsync();

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                var plainText = bodyNode?.InnerText ?? "No visible content found.";

                plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ").Trim();

                return plainText;
            }
            catch (Exception ex)
            {
                return $"An error occurred while scraping: {ex.Message}";
            }
        }
    }
}
