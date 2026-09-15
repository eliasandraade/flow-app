using Flow.Application.Auth;
using MediatR;

namespace Flow.Application.Auth.Commands.Register;

public record RegisterCommand(string Name, string Email, string Password) : IRequest<AuthResultDto>;
