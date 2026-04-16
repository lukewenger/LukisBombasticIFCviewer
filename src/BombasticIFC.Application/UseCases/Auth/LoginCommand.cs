using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Auth;

/// <summary>
/// Command for user login
/// </summary>
public record LoginCommand(string Username, string Password) : IRequest<AuthResponseDto>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException("Invalid username or password");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password");

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.Username, user.Email, user.Role.ToString());

        var refreshToken = _tokenService.GenerateRefreshToken();

        var tokenHash = _tokenService.HashToken(refreshToken);
        user.SetRefreshToken(tokenHash, DateTime.UtcNow.AddDays(7));
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new AuthResponseDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            accessToken,
            refreshToken
        );
    }
}
