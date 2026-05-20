namespace BeachTrip.Domain.Abstractions;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
