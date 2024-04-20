namespace LaborAI.EventSourceClient.DTOs;

public class CustomEventArgs : EventArgs
{
    public string? Type { get; set; }
    public string? Data { get; set; }
    public string? Id { get; set; }
    public int? Retry { get; set; }
}