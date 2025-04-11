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



        private async Task<IActionResult> ConnectToAzureOpenAI()
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
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");

            var messages = new List<ChatMessage>
            {

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
        }

        private async Task<IActionResult> ScrapeWeb()
        {
            var url = "https://en.wikipedia.org/wiki/Polystichum_setiferum";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Website blocked for scraping.");
            }

            var html = await response.Content.ReadAsStringAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
            var plainText = bodyNode?.InnerText ?? "No visible content found.";

            plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ").Trim();

            return Ok(plainText);
        }
    }
}
