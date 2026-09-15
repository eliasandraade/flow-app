using Flow.Application.Common.Exceptions;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Flow.Application.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
{
    private readonly UserManager<User> _userManager;
    private readonly AuthTokenIssuer _tokenIssuer;

    public RegisterCommandHandler(UserManager<User> userManager, AuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        // Public registration always creates an Operator. Elevated roles are granted by a
        // controlled administrative path, never by self-service.
        var user = User.Create(request.Name, request.Email, UserRole.Operator);
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new ValidationException(errors);
        }

        await _userManager.AddToRoleAsync(user, UserRole.Operator.ToString());

        var roles = await _userManager.GetRolesAsync(user);
        var (authResult, _) = await _tokenIssuer.IssueAsync(user, roles, cancellationToken);
        return authResult;
    }
}
