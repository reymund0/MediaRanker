using MediaRankerServer.Data;
using MediaRankerServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.Services
{
    public class UserService : IUserService
    {
        private readonly PostgreSQLContext _context;

        public UserService(PostgreSQLContext context)
        {
            _context = context;
        }

        public async Task<User?> Login(string username, string password, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

            if (user == null)
                return null;

            if (user.Password != password)
                return null;

            return user;
        }
    }
}
