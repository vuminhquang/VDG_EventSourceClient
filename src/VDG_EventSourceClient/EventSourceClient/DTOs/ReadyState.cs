namespace LaborAI.EventSourceClient.DTOs;

public enum ReadyState
{
    Initializing = -1,
    Connecting = 0,
    Open = 1,
    Closed = 2
}