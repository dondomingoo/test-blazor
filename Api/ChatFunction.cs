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

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var message = JsonSerializer.Deserialize<ChatMessage>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var response = req.CreateResponse();

        if (message is null || string.IsNullOrWhiteSpace(message.User) || string.IsNullOrWhiteSpace(message.Message))
        {
            response.StatusCode = System.Net.HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Invalid message.");
            return response;
        }

        // Sæt timestamp
        message.Timestamp = DateTime.Now;

        // Tilføj forsinkelse på 3 sekunder
        await Task.Delay(3000);

        messages.Add(message);

        response.StatusCode = System.Net.HttpStatusCode.OK;
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(messages));

        return response;
    }


    [Function("GetMessages")]
    public static HttpResponseData GetMessages(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
    FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ChatFunction");
        logger.LogInformation("Returning chat messages...");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json"); // Sikrer at header kun sættes én gang
        response.WriteString(JsonSerializer.Serialize(messages)); // Manuel serialisering

        return response;
    }
}
