using FluentValidation;
using MediatR;
using ReviewPlatform.Application.Common;
using ReviewPlatform.Domain.Common;

namespace ReviewPlatform.Application.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, string Role, bool IsActive, bool MustChangePassword);

public sealed record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;

internal sealed class ListUsersHandler(IIdentityService identity) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken) =>
        identity.ListUsersAsync(cancellationToken);
}

public sealed record CreateUserCommand(string Email, string FullName, string Role, string Password) : IRequest<UserDto>;

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Role).Must(Roles.All.Contains).WithMessage("Неизвестная роль.");
        RuleFor(c => c.Password).NotEmpty();
    }
}

internal sealed class CreateUserHandler(IIdentityService identity) : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var id = await identity.CreateUserAsync(request.Email, request.FullName, request.Role, request.Password, cancellationToken);
        return (await identity.FindUserAsync(id, cancellationToken))!;
    }
}

public sealed record UpdateUserCommand(Guid Id, string FullName, string Role, bool IsActive) : IRequest<UserDto>;

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Role).Must(Roles.All.Contains).WithMessage("Неизвестная роль.");
    }
}

internal sealed class UpdateUserHandler(IIdentityService identity, ICurrentUser currentUser) : IRequestHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == currentUser.UserId && (!request.IsActive || request.Role != Roles.Admin))
        {
            throw new DomainException("Нельзя заблокировать себя или снять с себя роль администратора.");
        }

        await identity.UpdateUserAsync(request.Id, request.FullName, request.Role, request.IsActive, cancellationToken);
        return (await identity.FindUserAsync(request.Id, cancellationToken))!;
    }
}

public sealed record ResetPasswordCommand(Guid Id, string NewPassword) : IRequest;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator() => RuleFor(c => c.NewPassword).NotEmpty();
}

internal sealed class ResetPasswordHandler(IIdentityService identity) : IRequestHandler<ResetPasswordCommand>
{
    public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        identity.ResetPasswordAsync(request.Id, request.NewPassword, cancellationToken);
}
