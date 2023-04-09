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
    private readonly int _fileSizeThreshold;
    private readonly int _thresholdForObjects;
    private readonly int _maxStart;
    private readonly int _maxEnd;

    private readonly string _receivedFilePath = Path.GetFullPath(
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
        _fileSizeThreshold = configuration.GetValue<int>("File.Gigabyte.Size.Threshold");
        _thresholdForObjects = configuration.GetValue<int>("ThresholdForObjects");
        _maxStart = _thresholdForObjects + 1;
        _maxEnd = _thresholdForObjects * 2;

        if (!File.Exists(_receivedFilePath))
        {
            File.Create(
                Path.GetFullPath(
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        @"received_objects.txt"
                    )
                )
            );
        }
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
                    float receivedFileSizeGigaByte = 0;

                    while (receivedFileSizeGigaByte < _fileSizeThreshold)
                    {
                        receivedFileSizeGigaByte = await GetReceivedFileSize(client, stoppingToken);
                    }

                    File.Delete(_receivedFilePath);
                    File.Create(
                        Path.GetFullPath(
                            Path.Combine(
                                Directory.GetCurrentDirectory(),
                                @"received_objects.txt"
                            )
                        )
                    );
                    _logger.LogInformation("File received_objects.txt was deleted and created again. Start again!");
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Exception message: {message}", e.Message);
                }
            }

            _logger.LogInformation("Worker stopped!");
        }
    }

    /// <summary>
    /// Получение информации о размере файла
    /// </summary>
    /// <param name="client">HttpClient для отправки запроса</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    /// <returns>Размер файла в GB (гигабайт)</returns>
    private async Task<int> GetReceivedFileSize(HttpClient client, CancellationToken stoppingToken)
    {
        int fileSizeGb = (int) SizeConverterHelper.BytesToGigabytes(new FileInfo(_receivedFilePath).Length);

        if (stoppingToken.IsCancellationRequested) return fileSizeGb;
        
        try
        {
            string responseBody = await GetObjects(client, stoppingToken);
            await SaveReceivedObjects(responseBody, stoppingToken);

            // Получим информацию о размере после того, как добавили новые данные
            long fileSize = new FileInfo(_receivedFilePath).Length;

            _logger.LogInformation(
                "Received information save in file. Current time -> {time}\nFile size is {bytes} Bytes {kilobyte} KB -> {megabyte} MB -> {gigabyte} GB",
                DateTimeOffset.Now,
                fileSize,
                SizeConverterHelper.BytesToKilobytes(fileSize),
                SizeConverterHelper.BytesToMegabytes(fileSize),
                SizeConverterHelper.BytesToGigabytes(fileSize)
            );
            
            fileSizeGb = (int) SizeConverterHelper.BytesToGigabytes(fileSize);
        }
        catch (Exception e)
        {
            _logger.LogInformation("Error!");
        }

        return fileSizeGb;
    }

    /// <summary>
    /// Получение данных из конечной точки API
    /// </summary>
    /// <param name="client">HttpClient для отправки запроса</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    /// <returns>Полученные от сервера данные в виде json</returns>
    private async Task<string> GetObjects(HttpClient client, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return "";
        
        int min = Random.Shared.Next(1, _thresholdForObjects);
        int max = Random.Shared.Next(_maxStart, _maxEnd);

        HttpResponseMessage response =
            await client.GetAsync($"https://{_apiHost}:{_apiPort}/objects?min={min}&max={max}", stoppingToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(stoppingToken);
    }

    /// <summary>
    /// Сохранение полученных данных в файле
    /// </summary>
    /// <param name="response">Данные</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    private async Task SaveReceivedObjects(string response, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        GenerateObjectModel[]? objectModels = JsonSerializer.Deserialize<GenerateObjectModel[]>(response, options);

        using (StreamWriter writer = new StreamWriter(_receivedFilePath, true, Encoding.UTF8))
        {
            if (objectModels != null)
                foreach (GenerateObjectModel item in objectModels)
                {
                    await writer.WriteLineAsync(item.Text);
                }

            writer.Close();
        }
    }
}

//     - сделать сортировку
//     - сохранить
//     - начать заново