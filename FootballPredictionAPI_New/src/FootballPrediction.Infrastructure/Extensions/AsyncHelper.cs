using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FootballPrediction.Infrastructure.Extensions;

public static class AsyncHelper
{
    /// <summary>
    /// Executes an asynchronous operation with proper async/await support
    /// </summary>
    public static async Task ExecuteAsync(Func<Task> action)
    {
        await action();
    }

    /// <summary>
    /// Executes a database operation asynchronously with proper error handling
    /// </summary>
    public static async Task<T> ExecuteDbOperationAsync<T>(DbContext context, Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            throw new Exception($"Database operation failed: {ex.Message}", ex);
        }
    }
}
