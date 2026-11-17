using Defra_People_API.Entities;

namespace Defra_People_API.Services.Logger;

// Drains the queue of API-call audit entries (consumer, endpoint, user, params, response, runtime)
// written by ApiLoggerFilter. Entries are written through the standard logging pipeline (console /
// Application Insights when configured) instead of a SQL database - there is no SQL dependency here.
public class LogProcessingService : BackgroundService
{
    private readonly ILogQueue _logQueue;
    private readonly ILogger<LogProcessingService> _logger;

    public LogProcessingService(ILogQueue logQueue, ILogger<LogProcessingService> logger)
    {
        _logQueue = logQueue;
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
                    WriteLogItem(logItem!);
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

    private void WriteLogItem(LogItem logItem)
    {
        var level = logItem.LogLevel switch
        {
            Entities.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            Entities.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        _logger.Log(level,
            "API call audit: {Consumer} {Method} {Endpoint} by {User} params={Params} response={Response} runtime={RuntimeMs}ms at {Timestamp:o}",
            logItem.Consumer, logItem.Method, logItem.Endpoint, logItem.User, logItem.Params, logItem.Response,
            logItem.Runtime.TotalMilliseconds, logItem.Timestamp);
    }
}
