using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Estoka.Application.Common;

/// <summary>
/// Traces e métricas de negócio (System.Diagnostics, sem dependência do OpenTelemetry na Application).
/// A Web registra a fonte e o medidor no OpenTelemetry e exporta via OTLP quando configurado.
/// </summary>
public static class EstokaTelemetry
{
    public const string Name = "Estoka";

    public static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> OrdersConfirmed =
        Meter.CreateCounter<long>("estoka.orders.confirmed", unit: "{pedido}", description: "Pedidos confirmados");
    public static readonly Counter<long> OrdersCanceled =
        Meter.CreateCounter<long>("estoka.orders.canceled", unit: "{pedido}", description: "Pedidos cancelados");
    public static readonly Counter<double> Revenue =
        Meter.CreateCounter<double>("estoka.orders.revenue", unit: "BRL", description: "Valor dos pedidos confirmados");
    public static readonly Counter<long> StockMovements =
        Meter.CreateCounter<long>("estoka.stock.movements", unit: "{movimentação}", description: "Movimentações de estoque registradas");
    public static readonly Counter<long> ConcurrencyRetries =
        Meter.CreateCounter<long>("estoka.concurrency.retries", unit: "{tentativa}", description: "Operações refeitas por conflito de concorrência (xmin)");
}
