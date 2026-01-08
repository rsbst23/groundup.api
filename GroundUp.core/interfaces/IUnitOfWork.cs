namespace GroundUp.core.interfaces;

/// <summary>
/// Cross-layer unit of work abstraction to run operations in an EF execution strategy + transaction.
/// Lives in core so services can request it without depending on EF types.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
