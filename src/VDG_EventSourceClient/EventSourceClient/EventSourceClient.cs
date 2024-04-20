using System.Runtime.CompilerServices;
using System.Text;
using LaborAI.EventSourceClient.DTOs;
using Microsoft.Extensions.Logging;

namespace LaborAI.EventSourceClient;

public class EventSourceClient : IAsyncEnumerable<CustomEventArgs>
{
    private readonly string _url;
    private readonly Dictionary<string, string> _headers;
    private readonly string _payload;
    private readonly string _method;
    private readonly bool _debug;

    private readonly HttpClient _httpClient;
    private readonly ILogger<EventSourceClient> _logger;
    private ReadyState _readyState = ReadyState.Initializing;

    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);

    public event EventHandler<CustomEventArgs>? EventReceived;
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public EventSourceClient(string url, HttpClient httpClient, ILogger<EventSourceClient> logger, EventSourceExtraOptions? options = null)
    {
        _url = url;
        _httpClient = httpClient;
        _logger = logger;
        _headers = options?.Headers ?? new Dictionary<string, string>();
        _payload = options?.Payload ?? string.Empty;
        _method = options?.Method ?? (string.IsNullOrEmpty(_payload) ? "GET" : "POST");
        _debug = options?.Debug ?? false;
        _maxRetries = options?.MaxRetries ?? 3;
    }

    protected virtual void OnEventReceived(CustomEventArgs e)
    {
        EventReceived?.Invoke(this, e);
    }

    protected virtual void OnStateChanged(StateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    private void SetReadyState(ReadyState state)
    {
        _readyState = state;
        OnStateChanged(new StateChangedEventArgs { ReadyState = _readyState });
    }

    public async Task Stream(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_debug)
            {
                _logger.LogInformation("Starting to stream events.");
            }

            await foreach (var eventArgs in StreamEvents(cancellationToken))
            {
                if (_debug)
                {
                    _logger.LogInformation($"Event received: {eventArgs.Type}");
                }

                OnEventReceived(eventArgs);
            }
        }
        catch (Exception ex)
        {
            if (_debug)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }

            throw;
        }
        finally
        {
            if (_debug)
            {
                _logger.LogInformation("Streaming has ended.");
            }
        }
    }

    public async IAsyncEnumerator<CustomEventArgs> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var eventArgs in StreamEvents(cancellationToken))
        {
            yield return eventArgs;
        }
    }

    private async IAsyncEnumerable<CustomEventArgs> StreamEvents([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SetReadyState(ReadyState.Connecting);
        HttpResponseMessage? response = null;
        var attempt = 0;

        while (attempt < _maxRetries)
        {
            try
            {
                var request = new HttpRequestMessage(new HttpMethod(_method), _url);
                foreach (var header in _headers.Where(header => header.Key != "Content-Type"))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrEmpty(_payload))
                {
                    var content = new StringContent(_payload, Encoding.UTF8, "application/json");
                    request.Content = content;
                }

                if (_debug)
                {
                    _logger.LogInformation($"Request method: {_method}");
                    _logger.LogInformation($"Request URL: {_url}");
                    _logger.LogInformation($"Headers: {string.Join(", ", _headers.Where(h => h.Key != "Content-Type").Select(h => $"{h.Key}: {h.Value}"))}");
                    if (!string.IsNullOrEmpty(_payload))
                    {
                        _logger.LogInformation($"Payload: {_payload}");
                    }
                }

                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception: {ex.Message}");
                attempt++;
                if (attempt >= _maxRetries) throw;
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }

        // Stream processing
        if (response != null)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            SetReadyState(ReadyState.Open);

            var chunk = new StringBuilder();
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) continue;

                if (line.StartsWith("data:"))
                {
                    chunk.AppendLine(line.Substring(5));
                }
                else if (line.Trim() == "" && chunk.Length > 0)
                {
                    var data = chunk.ToString().Trim();
                    chunk.Clear();
                    if (_debug)
                    {
                        _logger.LogInformation($"Raw event data: {data}");
                    }
                    yield return new CustomEventArgs { Type = "message", Data = data };
                }
            }
        }

        SetReadyState(ReadyState.Closed);
    }
}