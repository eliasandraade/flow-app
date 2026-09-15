using MediatR;

namespace Flow.Application.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest;
