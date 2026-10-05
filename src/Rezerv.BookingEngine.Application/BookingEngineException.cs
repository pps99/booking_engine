namespace Rezerv.BookingEngine.Application;

public sealed class BookingEngineException(
    string code,
    string message,
    int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
