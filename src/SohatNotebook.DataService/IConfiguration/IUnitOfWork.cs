using SohatNotebook.DataService.IRepository;

namespace SohatNotebook.DataService.IConfiguration;

public interface IUnitOfWork
{
    IUsersRepository Users { get; }

    Task CompleteAsync();
}