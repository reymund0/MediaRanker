using MediaRankerServer.Data.Entities;

namespace MediaRankerServer.Services
{
    public interface IUserService
    {
        Task<User?> Login(string username, string password, CancellationToken cancellationToken = default);
    }
}
