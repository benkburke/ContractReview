using ContractReview.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ContractReview.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<Contract> Contracts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
