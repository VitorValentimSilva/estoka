using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Estoka.Application.Security;
using Estoka.Domain.Auditing;
using Estoka.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Estoka.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Antes de cada SaveChanges: preenche CreatedAt/By e UpdatedAt/By, converte exclusões em soft delete
/// e grava um <see cref="AuditLog"/> com os valores antes/depois (somente campos alterados) na mesma transação.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
        nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletedBy), "Version",
    ];

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = clock.GetUtcNow();
        var userName = currentUser.UserName;
        var suppress = context is AppDbContext { SuppressAudit: true };
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged) continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Criado,
                EntityState.Deleted => AuditAction.Excluido,
                _ => AuditAction.Alterado,
            };

            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = now;
                softDeletable.DeletedBy = userName;
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.CreatedAt == default) auditable.CreatedAt = now;
                    auditable.CreatedBy ??= userName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userName;
                }

                if (!suppress && BuildLog(entry, action, userName, now) is { } log)
                    logs.Add(log);
            }
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private static AuditLog? BuildLog(EntityEntry entry, AuditAction action, string userName, DateTimeOffset now)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        IEnumerable<(string Name, PropertyEntry Prop)> props = entry.Properties.Select(p => (p.Metadata.Name, p));
        props = props.Concat(entry.ComplexProperties.SelectMany(c =>
            c.Properties.Select(p => ($"{c.Metadata.Name}.{p.Metadata.Name}", p))));

        foreach (var (name, prop) in props)
        {
            if (IgnoredProperties.Contains(prop.Metadata.Name) || prop.Metadata.IsShadowProperty()) continue;
            switch (action)
            {
                case AuditAction.Criado:
                    if (prop.CurrentValue is not null && name != "Id") newValues[name] = prop.CurrentValue;
                    break;
                case AuditAction.Alterado:
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                    {
                        oldValues[name] = prop.OriginalValue;
                        newValues[name] = prop.CurrentValue;
                    }
                    break;
                case AuditAction.Excluido:
                    if (name is not "IsDeleted" and not "Id") oldValues[name] = prop.OriginalValue;
                    break;
            }
        }

        if (action == AuditAction.Alterado && newValues.Count == 0) return null;
        if (action == AuditAction.Excluido) newValues["IsDeleted"] = true;

        return new AuditLog
        {
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "?",
            Action = action,
            OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            UserName = userName,
            Timestamp = now,
        };
    }
}
