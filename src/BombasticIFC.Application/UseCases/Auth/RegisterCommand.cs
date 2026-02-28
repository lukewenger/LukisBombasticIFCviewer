using BombasticIFC.Application.Common.Interfaces;
using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Entities;
using BombasticIFC.Domain.Enums;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Auth;

/// <summary>
/// Command for user registration
/// </summary>
public record RegisterCommand(string Username, string Email, string Password) : IRequest<AuthResponseDto>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Check if username already exists
        if (await _userRepository.ExistsAsync(request.Username, cancellationToken))
            throw new InvalidOperationException("Username is already taken");

        // Check if email already exists
        var existingByEmail = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingByEmail is not null)
            throw new InvalidOperationException("Email is already registered");

        // Hash password and create user
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = User.Create(request.Username, request.Email, passwordHash, UserRole.User);

        await _userRepository.AddAsync(user, cancellationToken);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.Username, user.Email, user.Role.ToString());

        var refreshToken = _tokenService.GenerateRefreshToken();

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
