using BombasticIFC.Application.DTOs;
using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Auth;

/// <summary>
/// Query to get the current user's profile
/// </summary>
public record GetCurrentUserQuery(Guid UserId) : IRequest<UserProfileDto>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException("User not found");

        return new UserProfileDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt
        );
    }
}
