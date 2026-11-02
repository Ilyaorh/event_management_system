namespace EventManagement.Core.Contracts;

public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
