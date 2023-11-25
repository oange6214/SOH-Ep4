using SohatNotebook.Entities.DbSet;

namespace SohatNotebook.DataService.IRepository;
public interface IRefreshTokensRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken> GetByRefreshToken(string refreshToken);
    Task<bool> MarkRefreshTokenAsUsed(RefreshToken refreshToken);
}
