using Estoka.Application.Common;
using Estoka.Application.Security;
using FluentValidation;

namespace Estoka.Application.Users;

/// <param name="IsProtected">Conta de demonstração: não pode ter papel/situação alterados enquanto a demo estiver ligada.</param>
public sealed record UserDto(string Id, string FullName, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt, bool IsProtected = false);

public sealed class CreateUserInput
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Vendedor;
    public string Password { get; set; } = string.Empty;
}

public sealed class CreateUserInputValidator : AbstractValidator<CreateUserInput>
{
    public CreateUserInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Informe o nome.").MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.").EmailAddress().WithMessage("E-mail inválido.").MaximumLength(150);
        RuleFor(x => x.Role).Must(r => Roles.UserRoles.Contains(r)).WithMessage("Papel inválido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha inicial.").MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.")
            .Matches("[A-Z]").WithMessage("A senha deve ter uma letra maiúscula.")
            .Matches("[a-z]").WithMessage("A senha deve ter uma letra minúscula.")
            .Matches("[0-9]").WithMessage("A senha deve ter um número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("A senha deve ter um caractere especial.");
    }
}

/// <summary>Gestão de usuários (somente Admin). Implementado na Infrastructure sobre o ASP.NET Core Identity.</summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<string> CreateAsync(CreateUserInput input, CancellationToken ct = default);
    Task ChangeRoleAsync(string userId, string role, CancellationToken ct = default);
    Task SetActiveAsync(string userId, bool active, CancellationToken ct = default);
}
