using Microsoft.Extensions.Configuration;

namespace WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _apiHost;
    private readonly string _apiPort;
    private readonly int _threadingNumber;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;

        _apiHost = configuration.GetValue<string>("ApiHost") ?? string.Empty;
        _apiPort = configuration.GetValue<string>("ApiPort") ?? string.Empty;
        _threadingNumber = configuration.GetValue<int>("ThreadingNumber");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}\nHost - > {host}, Port -> {port}, _threadingNumber -> {threadingNumber}", DateTimeOffset.Now, _apiHost, _apiPort, _threadingNumber);
            await Task.Delay(1000, stoppingToken);
        }
    }
}