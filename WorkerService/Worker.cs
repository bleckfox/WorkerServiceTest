using System.Text;
using System.Text.Json;
using WorkerService.Models;
using WorkerService.Helpers;

namespace WorkerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _apiHost;
    private readonly int _apiPort;
    private readonly int _threadingNumber;
    private readonly int _thresholdForObjects;
    private readonly int _maxStart;
    private readonly int _maxEnd;
    // private readonly string _receivedFileName = "received_objects.txt";
    private readonly string _receivedFilePath =
        Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(), 
                @"received_objects.txt"
            )
        );

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;

        _apiHost = configuration.GetValue<string>("ApiHost") ?? string.Empty;
        _apiPort = configuration.GetValue<int>("ApiPort");
        _threadingNumber = configuration.GetValue<int>("ThreadingNumber");
        _thresholdForObjects = configuration.GetValue<int>("ThresholdForObjects");
        _maxStart = _thresholdForObjects + 1;
        _maxEnd = _thresholdForObjects * 2;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (HttpClient client = new HttpClient())
        {
            _logger.LogInformation("Worker started!");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int min = Random.Shared.Next(1, _thresholdForObjects);
                    int max = Random.Shared.Next(_maxStart, _maxEnd);
                    
                    HttpResponseMessage response = await client.GetAsync($"https://{_apiHost}:{_apiPort}/objects?min={min}&max={max}", stoppingToken);

                    response.EnsureSuccessStatusCode();
                    
                    string responseBody = await response.Content.ReadAsStringAsync(stoppingToken);
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    GenerateObjectModel[]? objectModels = JsonSerializer.Deserialize<GenerateObjectModel[]>(responseBody, options);

                    using (StreamWriter writer = new StreamWriter(_receivedFilePath, true, Encoding.UTF8))
                    {
                        if (objectModels != null)
                            foreach (GenerateObjectModel item in objectModels)
                            {
                                await writer.WriteLineAsync(item.Text);
                            }
                        writer.Close();
                    }
                    
                    long fileSize = new FileInfo(_receivedFilePath).Length;
                    
                    _logger.LogInformation(
                        "Received information save in file. Current time -> {time}\nFile size is {bytes} Bytes {kilobyte} KB -> {megabyte} MB -> {gigabyte} GB", 
                        DateTimeOffset.Now,
                        fileSize,
                        SizeConverterHelper.BytesToKilobytes(fileSize),
                        SizeConverterHelper.BytesToMegabytes(fileSize),
                        SizeConverterHelper.BytesToGigabytes(fileSize)
                        );

                    await Task.Delay(10000, stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Error!");
                }
            }
            _logger.LogInformation("Worker stopped!");
        }
    }
}

//     - если больше 8 Гб, остановить запросы
//     - сделать сортировку
//     - сохранить
//     - начать заново