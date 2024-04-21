**EventSourceClient: Robust Server-Sent Events (SSE) Client for .NET**

The `EventSourceClient` library offers a comprehensive and easy-to-use implementation for consuming Server-Sent Events (SSE) in .NET applications. It's designed to efficiently handle real-time data streams, making it ideal for applications requiring seamless live updates like stock tickers, news feeds, or live notifications.

**Features:**

- **Seamless Integration:** Easily integrate with any .NET project to receive server-sent events.
- **Resilience and Reliability:** Features automatic reconnection logic to keep your application responsive and up-to-date despite network interruptions.
- **Asynchronous Design:** Utilizes an asynchronous API to ensure non-blocking operations, perfectly suited for modern C# applications.
- **Flexible Configuration:** Offers customizable options to alter client behavior to fit your needs, including custom headers for initial requests and adjustable reconnection times.
- **Event Filtering:** Capable of filtering and responding to specific event types directly from the server.
- **Logging Support:** Comes with integrated logging capabilities to assist in debugging and monitoring the client's behavior in production environments.
- **Cross-Platform:** Compatible with any platform that supports .NET Standard, including Windows, Linux, and macOS.

**Getting Started:**

To start receiving SSE, initialize the `EventSourceClient` with the server URL, configure your event handlers, and begin streaming. The client manages all connection and stream parsing aspects.

```csharp
var eventSourceClient = new EventSourceClient(url, new HttpClient(), logger, new EventSourceExtraOptions());
Console.WriteLine("Client is now listening to the SSE server...");

eventSourceClient.EventReceived += (sender, e) => {
    Console.WriteLine($"Received Event: {e.Type}");
    Console.WriteLine($"Data: {e.Data}");
    Console.WriteLine($"ID: {e.Id}");
    if (e.Retry.HasValue) {
        Console.WriteLine($"Retry: {e.Retry.Value}");
    }
};

eventSourceClient.StateChanged += (sender, e) => Console.WriteLine($"State Changed: {e.ReadyState}");

await eventSourceClient.Stream();
```

**AsyncEnumerable Sample:**

The `EventSourceClient` also provides an `AsyncEnumerable` method for streaming events asynchronously. Here's an example of how to use it:

```csharp
var eventSourceClient = new EventSourceClient(url, new HttpClient(), logger, new EventSourceExtraOptions());
Console.WriteLine("Client is now listening to the SSE server...");

await foreach (var eventArgs in eventSourceClient.StreamAsyncEnumerable())
{
    Console.WriteLine($"Received Event: {eventArgs.Type}");
    Console.WriteLine($"Data: {eventArgs.Data}");
    Console.WriteLine($"ID: {eventArgs.Id}");
}
```

In this example, we initialize the `EventSourceClient` as before. Instead of using the `Stream` method and handling events through event handlers, we use the `StreamAsyncEnumerable` method to asynchronously iterate over the events using the `await foreach` construct.

Each event is represented by a `CustomEventArgs` object, which contains the event type, data, and ID. You can process each event as needed within the loop.

Using the `AsyncEnumerable` approach provides a more concise and readable way to handle events, especially when you don't need to handle state changes or other events separately.

**Use Cases:**

- Real-time dashboards
- Live notifications in web and desktop applications
- Financial applications requiring live updates of market data
- Any application needing to efficiently consume live data feeds

**Documentation:**

For further details, please see the unit tests and sample code provided with the library.

---