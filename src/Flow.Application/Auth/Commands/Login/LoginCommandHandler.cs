using Flow.Application.Common.Exceptions;
using Flow.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Flow.Application.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly UserManager<User> _userManager;
    private readonly AuthTokenIssuer _tokenIssuer;

    public LoginCommandHandler(UserManager<User> userManager, AuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedException("Invalid credentials.");

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid) throw new UnauthorizedException("Invalid credentials.");

        var roles = await _userManager.GetRolesAsync(user);
        var (result, _) = await _tokenIssuer.IssueAsync(user, roles, cancellationToken);
        return result;
    }
}
