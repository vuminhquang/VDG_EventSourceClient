using System.Net;
using System.Text;
using LaborAI.EventSourceClient;
using LaborAI.EventSourceClient.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace EventSourceClientTests;

[TestFixture]
public class EventSourceClientTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private Mock<ILogger<EventSourceClient>> _mockLogger; // Mock logger
    private EventSourceClient _eventSourceClient;
    private const string TestUrl = "http://test.com/events";
    private EventSourceExtraOptions _options;
    
    [SetUp]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<EventSourceClient>>(); // Initialize the mock logger
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _options = new EventSourceExtraOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer test_token" }
            },
            Payload = "{\"query\": \"test\"}",
            Method = "POST",
            Debug = true,
            MaxRetries = 3
        };
        _eventSourceClient = new EventSourceClient(TestUrl, httpClient, _mockLogger.Object, _options);
    }
    
    [TearDown]
    public void Teardown()
    {
        _eventSourceClient.Dispose();
    }

    [Test]
    public async Task ShouldReceiveEventsAndChangeState()
    {
        var responseContent =
            new StringContent("data: {\"message\": \"Hello\"}\n\n", Encoding.UTF8, "text/event-stream");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var eventReceived = false;
        _eventSourceClient.EventReceived += (sender, args) =>
        {
            eventReceived = true;
            Assert.That(args.Data, Is.EqualTo("{\"message\": \"Hello\"}"));
        };

        var stateChangedToOpen = false;
        _eventSourceClient.StateChanged += (sender, args) =>
        {
            if (args.ReadyState == ReadyState.Open)
                stateChangedToOpen = true;
        };

        await _eventSourceClient.Stream();

        Assert.Multiple(() =>
        {
            Assert.That(eventReceived, Is.True);
            Assert.That(stateChangedToOpen, Is.True);
        });
    }

    [Test]
    public void ShouldHandleNetworkErrorsGracefully()
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        Assert.ThrowsAsync<HttpRequestException>(async () => await _eventSourceClient.Stream());
    }

    [Test]
    public async Task ShouldHandleMultipleEventsInSingleStream()
    {
        var responseContent = new StringContent("event: customEvent1\ndata: {\"message\": \"Event 1\"}\n\n" +
                                                "event: customEvent2\ndata: {\"message\": \"Event 2\"}\n\n",
            Encoding.UTF8, "text/event-stream");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var receivedEvents = new List<CustomEventArgs>();
        _eventSourceClient.EventReceived += (sender, args) => { receivedEvents.Add(args); };

        await _eventSourceClient.Stream();

        Assert.That(receivedEvents, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(receivedEvents[0].Data, Is.EqualTo("{\"message\": \"Event 1\"}"));
            Assert.That(receivedEvents[1].Data, Is.EqualTo("{\"message\": \"Event 2\"}"));
        });
    }

    [Test]
    public async Task ShouldCorrectlyHandlePostMethodWithPayload()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post && req.Content != null), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        _eventSourceClient = new EventSourceClient(TestUrl, new HttpClient(_mockHttpMessageHandler.Object),
            _mockLogger.Object,  // Pass the mock logger here
            new EventSourceExtraOptions
            {
                Method = "POST",
                Payload = "{\"query\": \"mutation { addEvent { id } }\"}",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            });

        // Assuming Stream method does something with POST and payload
        await _eventSourceClient.Stream();

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.Content!.ReadAsStringAsync().Result == "{\"query\": \"mutation { addEvent { id } }\"}"
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Test]
    public async Task ShouldRetryOnTimeout()
    {
        var callCount = 0;
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                callCount++;
                if (callCount < _options.MaxRetries)
                {
                    await Task.Delay(1000, cancellationToken); // Simulate delay
                    throw new TaskCanceledException("Timeout");
                }

                return responseMessage;
            });

        await _eventSourceClient.Stream();

        Assert.That(callCount, Is.EqualTo(_options.MaxRetries), "The method should retry the number of times specified in MaxRetries.");
    }
}