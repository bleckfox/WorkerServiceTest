using RestSharp;

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
        using (HttpClient client = new HttpClient())
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int min = Random.Shared.Next(1, 5);
                    int max = Random.Shared.Next(min + 1, min + 5);
                    
                    HttpResponseMessage response =
                        await client.GetAsync($"https://{_apiHost}:{_apiPort}/objects?min={min}&max={max}");
                    
                    response.EnsureSuccessStatusCode();
                    
                    string responseBody = await response.Content.ReadAsStringAsync();
                    
                    _logger.LogInformation($"{responseBody}");
                    
                    await Task.Delay(10000, stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Error!");
                }
            }
        }
    }
}
//     - если не существует, создать файл received_objects.txt
//     - записать полученные значения по значению на строку
//     - сохранить
//     - проверять размер файла
//     - если больше 8 Гб, остановить запросы
//     - сделать сортировку
//     - сохранить
//     - начать заново