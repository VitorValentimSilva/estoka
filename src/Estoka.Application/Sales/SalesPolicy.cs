namespace Estoka.Application.Sales;

/// <summary>Regras comerciais configuradas no código (simples e testáveis).</summary>
public static class SalesPolicy
{
    /// <summary>Desconto máximo (itens + pedido) sobre o valor bruto para quem não tem <c>GrantUnlimitedDiscount</c>.</summary>
    public const decimal MaxSellerDiscountPercent = 10m;
}
