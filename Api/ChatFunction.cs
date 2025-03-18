using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BlazorApp.Shared;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

public static class ChatFunction
{
    private static readonly List<ChatMessage> messages = new();

    [Function("SendMessage")]
    public static async Task<HttpResponseData> SendMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ChatFunction");
        logger.LogInformation("Processing chat message...");

        // Læs body og deserialiser
        string requestBody;
        using (var reader = new StreamReader(req.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }
        var message = JsonSerializer.Deserialize<ChatMessage>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var response = req.CreateResponse();

        if (message is null || string.IsNullOrWhiteSpace(message.User) || string.IsNullOrWhiteSpace(message.Message))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Invalid message.");
            return response;
        }

        messages.Add(message);

        response.StatusCode = HttpStatusCode.OK;
        response.Headers.Add("Content-Type", "application/json");
        response.Headers.Add("Access-Control-Allow-Origin", "*"); // 🔥 CORS FIX!
        await response.WriteAsJsonAsync(messages);
        return response;
    }

    [Function("GetMessages")]
    public static async Task<HttpResponseData> GetMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ChatFunction");
        logger.LogInformation("Returning chat messages...");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.Headers.Add("Access-Control-Allow-Origin", "*"); // 🔥 CORS FIX!

        await response.WriteAsJsonAsync(messages);
        return response;
    }
}
