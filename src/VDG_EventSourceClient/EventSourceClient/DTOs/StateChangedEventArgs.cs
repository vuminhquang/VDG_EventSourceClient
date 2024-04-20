namespace LaborAI.EventSourceClient.DTOs;

public class StateChangedEventArgs : EventArgs
{
    public ReadyState ReadyState { get; init; }
}