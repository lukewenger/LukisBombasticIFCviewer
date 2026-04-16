using BombasticIFC.Domain.Repositories;
using MediatR;

namespace BombasticIFC.Application.UseCases.Auth;

/// <summary>
/// Command for logging out a user by invalidating their stored refresh token.
/// </summary>
public record LogoutCommand(Guid UserId) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IUserRepository _userRepository;

    public LogoutCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return;

        user.ClearRefreshToken();
        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
