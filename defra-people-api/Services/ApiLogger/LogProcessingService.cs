using Microsoft.Data.SqlClient;
using Defra_People_API.Entities;
using Defra_People_API.Repository;
using Microsoft.EntityFrameworkCore;

namespace Defra_People_API.Services.Logger;

public class LogProcessingService : BackgroundService
{
    private readonly ILogQueue _logQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogProcessingService> _logger;
    
    public LogProcessingService(ILogQueue logQueue, IServiceProvider serviceProvider, ILogger<LogProcessingService> logger)
    {
        _logQueue = logQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Log Processing Service is starting");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (_logQueue.TryDequeue(out var logItem))
                {
                    await ProcessLogItemWithRetry(logItem, stoppingToken);
                }
                
                // Wait a bit before checking for new items
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in log processing service");
                
                // wait before continuing after error
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        
        _logger.LogInformation("Log Processing Service is stopping");
    }
    
    private async Task ProcessLogItemWithRetry(LogItem logItem, CancellationToken stoppingToken)
    {
        int retryCount = 1;
        const int maxRetries = 5;
        TimeSpan delay = TimeSpan.FromSeconds(10);
        
        while (true)
        {
            try
            {
                // get a fresh context for each retry
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LogDbContext>();
                
                dbContext.ApiLogs.Add(logItem);
                await dbContext.SaveChangesAsync(stoppingToken);
                
                _logger.LogDebug($"Successfully saved log item for {logItem.Endpoint} on attempt {retryCount}");
                return; 
            }
            catch (Exception ex) 
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to save log item after {RetryCount} attempts", retryCount);
                    throw; 
                }
                _logger.LogWarning(ex, "Failed to get last change date (attempt {current}/{total}), waiting 10 seconds before retrying...", retryCount, maxRetries);
                // Exponential backoff
                await Task.Delay(delay, stoppingToken);
                var lastDelay = Math.Round(delay.TotalSeconds, 0);
                delay = TimeSpan.FromSeconds(Math.Pow(1.2, lastDelay));
            }
        }
    }
}
