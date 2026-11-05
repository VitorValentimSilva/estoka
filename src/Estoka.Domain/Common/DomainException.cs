namespace Estoka.Domain.Common;

/// <summary>Violação de regra de negócio. A mensagem é amigável e pode ser exibida ao usuário.</summary>
public class DomainException(string message) : Exception(message);

public sealed class InsufficientStockException(string productName, decimal available, decimal requested)
    : DomainException($"Estoque insuficiente para \"{productName}\": disponível {available:0.###}, solicitado {requested:0.###}.")
{
    public string ProductName { get; } = productName;
    public decimal Available { get; } = available;
    public decimal Requested { get; } = requested;
}

public sealed class InvalidStatusTransitionException(string message) : DomainException(message);
