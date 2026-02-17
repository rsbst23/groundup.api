namespace GroundUp.Data.Abstractions.Interfaces;

/// <summary>
/// Unit of work abstraction to run operations in an execution strategy + transaction.
/// Implemented by the data layer (EF Core) and used by services.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
