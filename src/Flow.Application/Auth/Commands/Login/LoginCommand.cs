using Flow.Application.Auth;
using MediatR;

namespace Flow.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResultDto>;
