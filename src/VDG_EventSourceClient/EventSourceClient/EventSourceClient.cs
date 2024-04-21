using System.Runtime.CompilerServices;
using System.Text;
using LaborAI.EventSourceClient.DTOs;
using Microsoft.Extensions.Logging;

namespace LaborAI.EventSourceClient;

public class EventSourceClient : IDisposable
{
    private readonly string? _url;
    private readonly Dictionary<string, string> _headers;
    private readonly string? _payload;
    private readonly string? _method;
    private readonly bool _debug;
    private readonly HttpContent? _content;
    private readonly HttpClient? _httpClient;
    private readonly ILogger<EventSourceClient>? _logger;
    private ReadyState _readyState = ReadyState.Initializing;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
    private bool _disposed;

    public event EventHandler<CustomEventArgs>? EventReceived;
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public EventSourceClient(string url, HttpClient httpClient, ILogger<EventSourceClient>? logger, EventSourceExtraOptions? options = null)
        : this(httpClient, logger, options)
    {
        _url = url;
        _method = options?.Method ?? (string.IsNullOrEmpty(_payload) ? "GET" : "POST");
    }

    public EventSourceClient(HttpContent content, ILogger<EventSourceClient>? logger, EventSourceExtraOptions? options = null)
        : this(httpClient: null, logger, options)
    {
        _content = content;
    }

    private EventSourceClient(HttpClient? httpClient, ILogger<EventSourceClient>? logger, EventSourceExtraOptions? options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _headers = options?.Headers ?? new Dictionary<string, string>();
        _payload = options?.Payload;
        _debug = options?.Debug ?? false;
        _maxRetries = options?.MaxRetries ?? 3;
    }

    protected virtual void OnEventReceived(CustomEventArgs e) => EventReceived?.Invoke(this, e);
    protected virtual void OnStateChanged(StateChangedEventArgs e) => StateChanged?.Invoke(this, e);

    private void SetReadyState(ReadyState state)
    {
        _readyState = state;
        OnStateChanged(new StateChangedEventArgs { ReadyState = _readyState });
    }

    private void LogDebug(string message)
    {
        if (_debug)
        {
            _logger?.LogInformation(message);
        }
    }

    private void LogError(string message, Exception ex)
    {
        if (_debug)
        {
            _logger?.LogError(ex, message);
        }
    }

    public async Task Stream(CancellationToken cancellationToken = default)
    {
        try
        {
            LogDebug("Starting to stream events.");
            await StreamEvents(cancellationToken);
        }
        catch (Exception ex)
        {
            LogError("An error occurred: " + ex.Message, ex);
            throw;
        }
        finally
        {
            LogDebug("Streaming has ended.");
        }
    }

    private async Task StreamEvents(CancellationToken cancellationToken = default)
    {
        SetReadyState(ReadyState.Connecting);
        Stream? stream = null;
        var attempt = 0;

        while (attempt < _maxRetries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                stream = await PrepareStream(cancellationToken);
                break;
            }
            catch (Exception ex)
            {
                LogError($"Exception: {ex.Message}", ex);
                attempt++;
                if (attempt >= _maxRetries) throw;
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }

        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            SetReadyState(ReadyState.Open);
            await ProcessStream(reader, cancellationToken);
        }

        SetReadyState(ReadyState.Closed);
    }

    private async Task<Stream> PrepareStream(CancellationToken cancellationToken)
    {
        if (_content != null)
        {
            return await _content.ReadAsStreamAsync(cancellationToken);
        }
        else if (_httpClient != null && _url != null && _method != null)
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

            LogDebug($"Request method: {_method} | Request URL: {_url} | Headers: {string.Join(", ", _headers.Where(h => h.Key != "Content-Type").Select(h => $"{h.Key}: {h.Value}"))}");
            if (!string.IsNullOrEmpty(_payload))
            {
                LogDebug($"Payload: {_payload}");
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        throw new InvalidOperationException("Method or required parameters are not set for HTTP request.");
    }

    private async Task ProcessStream(StreamReader reader, CancellationToken cancellationToken)
    {
        var chunk = new StringBuilder();
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) continue;

            if (line.StartsWith("data:"))
            {
                chunk.AppendLine(line[5..].Trim());
            }
            else if (line.Trim() == "" && chunk.Length > 0)
            {
                var data = chunk.ToString().TrimEnd('\r', '\n');
                chunk.Clear();
                LogDebug($"Raw event data: {data}");
                OnEventReceived(new CustomEventArgs { Type = "message", Data = data });
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _content?.Dispose();
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}