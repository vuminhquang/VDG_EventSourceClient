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

// Section 1: Using URL and HttpClient
using (var eventSourceClient1 = new EventSourceClient(url, new HttpClient(), logger, new EventSourceExtraOptions()))
{

    Console.WriteLine("Client 1 is now listening to the SSE server...");

    eventSourceClient1.EventReceived += (sender, e) =>
    {
        Console.WriteLine($"Client 1 - Received Event: {e.Type}");
        Console.WriteLine($"Client 1 - Data: {e.Data}");
        Console.WriteLine($"Client 1 - ID: {e.Id}");
        if (e.Retry.HasValue)
        {
            Console.WriteLine($"Client 1 - Retry: {e.Retry.Value}");
        }
    };

    eventSourceClient1.StateChanged += (sender, e) =>
    {
        Console.WriteLine($"Client 1 - State Changed: {e.ReadyState}");
    };

    await eventSourceClient1.Stream();
}

Console.WriteLine("Create a new EventSourceClient using the HttpContent from the response...");

// Section 2: Using HttpContent from the SSE server
using var httpClient = new HttpClient();
var response = await httpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
response.EnsureSuccessStatusCode();

var eventSourceClient2 = new EventSourceClient(response.Content, logger);

Console.WriteLine("Client 2 is now listening to the SSE server...");

eventSourceClient2.EventReceived += (sender, e) =>
{
    Console.WriteLine($"Client 2 - Received Event: {e.Type}");
    Console.WriteLine($"Client 2 - Data: {e.Data}");
    Console.WriteLine($"Client 2 - ID: {e.Id}");
    if (e.Retry.HasValue)
    {
        Console.WriteLine($"Client 2 - Retry: {e.Retry.Value}");
    }
};

eventSourceClient2.StateChanged += (sender, e) =>
{
    Console.WriteLine($"Client 2 - State Changed: {e.ReadyState}");
};

await eventSourceClient2.Stream();

Console.WriteLine("Press ENTER to exit...");
Console.ReadLine();

sseServer.Stop();