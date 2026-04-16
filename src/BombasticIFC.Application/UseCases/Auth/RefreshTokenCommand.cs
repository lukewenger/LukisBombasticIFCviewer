using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Auth;

/// <summary>
/// Command for rotating a refresh token and issuing a new access + refresh token pair.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var user = await _userRepository.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            user.ClearRefreshToken();
            await _userRepository.UpdateAsync(user, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account deactivated");

        var newAccessToken = _tokenService.GenerateAccessToken(
            user.Id, user.Username, user.Email, user.Role.ToString());

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashToken(newRefreshToken);
        user.SetRefreshToken(newTokenHash, DateTime.UtcNow.AddDays(7));
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new AuthResponseDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            newAccessToken,
            newRefreshToken
        );
    }
}
