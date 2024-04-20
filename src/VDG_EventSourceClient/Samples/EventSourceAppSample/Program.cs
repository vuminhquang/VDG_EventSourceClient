using EventSourceAppSample;
using LaborAI.EventSourceClient;
using LaborAI.EventSourceClient.DTOs;
using Microsoft.Extensions.Logging;

ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var logger = loggerFactory.CreateLogger<EventSourceClient>();
            
var url = "http://localhost:5000/";
var sseServer = new SseServer(url);
sseServer.Start();

// Assuming EventSourceClient is properly implemented to handle SSE
var eventSourceClient = new EventSourceClient(url, new HttpClient(), logger, new EventSourceExtraOptions());

Console.WriteLine("Client is now listening to the SSE server...");

eventSourceClient.EventReceived += (sender, e) =>
{
    Console.WriteLine($"Received Event: {e.Type}");
    Console.WriteLine($"Data: {e.Data}");
    Console.WriteLine($"ID: {e.Id}");
    if (e.Retry.HasValue)
    {
        Console.WriteLine($"Retry: {e.Retry.Value}");
    }
};

eventSourceClient.StateChanged += (sender, e) =>
{
    Console.WriteLine($"State Changed: {e.ReadyState}");
};

await eventSourceClient.Stream();

Console.WriteLine("Press ENTER to exit...");
Console.ReadLine();

sseServer.Stop();