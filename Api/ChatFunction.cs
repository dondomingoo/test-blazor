using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static class ChatFunction
{
    private static readonly HttpClient httpClient = new();

    private const string RepoOwner = "dondomingoo";
    private const string RepoName = "chat-storage";
    private const string FilePath = "messages.json";
    private const string Branch = "main";
    private static string GitHubToken = Environment.GetEnvironmentVariable("GITHUB_CHAT_TOKEN");

    [Function("GetMessages")]
    public static async Task<HttpResponseData> GetMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ChatFunction");
        logger.LogInformation("Henter chatbeskeder fra GitHub...");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("token", GitHubToken);
            request.Headers.UserAgent.ParseAdd("BlazorApp");

            var apiResponse = await httpClient.SendAsync(request);
            var jsonResponse = await apiResponse.Content.ReadAsStringAsync();

            if (!apiResponse.IsSuccessStatusCode)
            {
                logger.LogError($"Fejl ved hentning fra GitHub: {apiResponse.StatusCode} - {jsonResponse}");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                return response;
            }

            var jsonDoc = JsonDocument.Parse(jsonResponse);
            if (!jsonDoc.RootElement.TryGetProperty("content", out var contentProperty))
            {
                logger.LogError("Ingen 'content' fundet i JSON-responsen.");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                return response;
            }

            byte[] data = Convert.FromBase64String(contentProperty.GetString());
            string messagesJson = Encoding.UTF8.GetString(data).Trim();

            if (string.IsNullOrWhiteSpace(messagesJson) || messagesJson == "[]")
            {
                messagesJson = "[]"; // Returnér en tom liste, hvis filen er tom
            }

            await response.WriteStringAsync(messagesJson);
        }
        catch (Exception ex)
        {
            logger.LogError($"Fejl ved hentning af beskeder: {ex.Message}");
            response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
        }

        return response;
    }

    [Function("SendMessage")]
    public static async Task<HttpResponseData> SendMessage(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
     FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ChatFunction");
        logger.LogInformation("Gemmer ny besked til GitHub...");

        var response = req.CreateResponse();

        try
        {
            string requestBody;
            using (var reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            logger.LogInformation($"📌 Raw JSON fra klient: '{requestBody}'");

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync("❌ Fejl: Request body er tom!");
                return response;
            }

            var message = JsonConvert.DeserializeObject<ChatMessage>(requestBody);

            if (message is null || string.IsNullOrWhiteSpace(message.User) || string.IsNullOrWhiteSpace(message.Message))
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync("❌ Ugyldig besked.");
                return response;
            }

            // 🔥 **Sæt timestamp, hvis det mangler**
            if (message.Timestamp == default)
            {
                message.Timestamp = DateTime.UtcNow;
                logger.LogInformation($"📌 Timestamp var tomt - Sat til: {message.Timestamp}");
            }

            logger.LogInformation($"📌 Deserialized message: User={message?.User}, Message={message?.Message}, Timestamp={message?.Timestamp}");

            // ✅ **HENT SHA-VÆRDI FØRST**
            string updateUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
            string fileSha = "";

            var fileInfoRequest = new HttpRequestMessage(HttpMethod.Get, updateUrl);
            fileInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("token", GitHubToken);
            fileInfoRequest.Headers.UserAgent.ParseAdd("BlazorApp");

            var fileInfoResponse = await httpClient.SendAsync(fileInfoRequest);
            var fileInfoContent = await fileInfoResponse.Content.ReadAsStringAsync();

            List<ChatMessage> messages = new();

            if (fileInfoResponse.IsSuccessStatusCode)
            {
                var fileInfoJson = JsonConvert.DeserializeObject<dynamic>(fileInfoContent);
                fileSha = fileInfoJson?.sha;
                logger.LogInformation($"📌 SHA for eksisterende fil: {fileSha}");

                // ✅ **HENT EKSISTERENDE BESKEDER**
                byte[] data = Convert.FromBase64String(fileInfoJson.content.ToString());
                string existingMessagesJson = Encoding.UTF8.GetString(data).Trim();

                if (!string.IsNullOrWhiteSpace(existingMessagesJson) && existingMessagesJson != "[]")
                {
                    messages = JsonConvert.DeserializeObject<List<ChatMessage>>(existingMessagesJson) ?? new List<ChatMessage>();
                }
            }
            else
            {
                logger.LogWarning("❗ Kunne ikke hente SHA - Filen eksisterer måske ikke endnu.");
            }

            // ✅ **TILFØJ NY BESKED**
            messages.Add(message);
            string newJson = JsonConvert.SerializeObject(messages, Formatting.Indented);

            // ✅ **OPRET PAYLOAD MED SHA**
            var updatePayload = new
            {
                message = "Opdateret chatlog",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(newJson)),
                branch = Branch,
                sha = fileSha // ✅ Tilføj SHA-værdi
            };

            var request = new HttpRequestMessage(HttpMethod.Put, updateUrl)
            {
                Content = new StringContent(JsonConvert.SerializeObject(updatePayload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("token", GitHubToken);
            request.Headers.UserAgent.ParseAdd("BlazorApp");

            var updateResponse = await httpClient.SendAsync(request);
            if (!updateResponse.IsSuccessStatusCode)
            {
                var errorResponse = await updateResponse.Content.ReadAsStringAsync();
                logger.LogError($"❌ Fejl ved GitHub PUT: {updateResponse.StatusCode} - {errorResponse}");
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                return response;
            }

            response.StatusCode = System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Fejl ved lagring af besked: {ex.Message}");
            response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
        }

        return response;
    }

}
