using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps command execution in a database transaction.
/// Only applies to commands (write operations), not queries.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands in transactions, not queries
        var isCommand = IsCommand();
        if (!isCommand)
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;

        // If there's already an active transaction, just proceed
        if (_unitOfWork.HasActiveTransaction)
        {
            return await next();
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            _logger.LogDebug(
                "Beginning transaction for {RequestName}",
                requestName);

            var response = await next();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogDebug(
                "Committed transaction for {RequestName}",
                requestName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing {RequestName}. Rolling back transaction.",
                requestName);

            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsCommand()
    {
        var requestType = typeof(TRequest);
        
        // Check if implements ICommand<TResponse> or ICommand
        return requestType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>) ||
            i == typeof(ICommand));
    }
}
