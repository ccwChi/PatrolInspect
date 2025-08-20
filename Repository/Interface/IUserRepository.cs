using PatrolInspect.Models;

namespace PatrolInspect.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<MesUser?> GetUserByUserNoAsync(string userNo);
        Task<(bool Success, string Message, MesUser? User)> ValidateUserLoginAsync(string userNo);
        Task<bool> TestConnectionAsync();
    }
}