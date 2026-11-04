namespace Estoka.Domain.Auditing;

public enum AuditAction
{
    Criado = 1,
    Alterado = 2,
    Excluido = 3,
}

/// <summary>Registro de auditoria gerado automaticamente pelo interceptor do EF Core.</summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public AuditAction Action { get; set; }
    /// <summary>JSON com os valores anteriores (somente campos alterados).</summary>
    public string? OldValues { get; set; }
    /// <summary>JSON com os novos valores (somente campos alterados).</summary>
    public string? NewValues { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
