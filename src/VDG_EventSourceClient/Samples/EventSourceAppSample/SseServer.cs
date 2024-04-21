namespace EventSourceAppSample;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal class SseServer
{
    private readonly HttpListener _listener;
    private readonly string _url;

    public SseServer(string url)
    {
        _url = url;
        _listener = new HttpListener();
        _listener.Prefixes.Add(url);
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(async () => await HandleIncomingConnections());
        Console.WriteLine($"Server started at {_url}");
    }

    private async Task HandleIncomingConnections()
    {
        while (_listener.IsListening)
        {
            var context = await _listener.GetContextAsync();
            var response = context.Response;
            response.ContentType = "text/event-stream";
            byte[] buffer;

            try
            {
                await using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8);
                var eventId = 1;
                var startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalSeconds < 30)  // Check if 30 seconds have passed
                {
                    var eventData = $"Event {eventId}";
                    buffer = Encoding.UTF8.GetBytes($"id: {eventId}\ndata: {eventData}\n\n");
                    await writer.WriteAsync(Encoding.UTF8.GetString(buffer));
                    await writer.FlushAsync();
                    eventId++;
                    Thread.Sleep(1000);  // Simulate an event every second
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection closed.");
            }
            finally
            {
                response.Close();
            }
        }
    }

    public void Stop()
    {
        _listener.Stop();
    }
}