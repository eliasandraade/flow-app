using Flow.Application.Auth;
using MediatR;

namespace Flow.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthResultDto>;
