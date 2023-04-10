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
    private readonly char[] _alphabet = "абвгдеёжзийклмнопрстуфхцчшщэюя".ToCharArray();

    private readonly string _receivedDirectoryPath =
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Received values"));

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started!");

        Thread[] threads = new Thread[_threadingNumber];

        // Первичный запуск потоков
        // for (int i = 0; i < _threadingNumber; i++)
        // {
        //     threads[i] = new Thread(_ =>
        //     {
        //         string receivedFilePath = Path.Combine(_receivedDirectoryPath, $"received_objects_{i}.txt");
        //         if (!File.Exists(receivedFilePath))
        //         {
        //             using (FileStream fs = new FileStream(receivedFilePath, FileMode.CreateNew))
        //             {
        //                 fs.Close();
        //             }
        //         }
        //         StartGettingData(receivedFilePath, stoppingToken).GetAwaiter();
        //     });
        //     threads[i].Start();
        // }
        
        // Создание файла, если не существует при запуске
        string receivedFilePath = Path.Combine(_receivedDirectoryPath, $"received_objects.txt");
        if (!File.Exists(receivedFilePath))
        {
            using (FileStream fs = new FileStream(receivedFilePath, FileMode.CreateNew))
            {
                fs.Close();
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // Проверка и перезапуск потоков, если завершили свою работу
            // for (int i = 0; i < _threadingNumber; i++)
            // {
            //     if (threads[i].ThreadState == ThreadState.Stopped)
            //     {
            //         threads[i] = new Thread(_ =>
            //         {
            //             string receivedFilePath = Path.Combine(_receivedDirectoryPath, $"received_objects_{i}.txt");
            //             if (!File.Exists(receivedFilePath))
            //             {
            //                 using (FileStream fs = new FileStream(receivedFilePath, FileMode.CreateNew))
            //                 {
            //                     fs.Close();
            //                 }
            //             }
            //             StartGettingData(receivedFilePath, stoppingToken).GetAwaiter();
            //         });
            //         threads[i].Start();
            //     }
            // }
            await StartGettingData(receivedFilePath, stoppingToken);
            await Task.Delay(5000, stoppingToken);
        }

        _logger.LogInformation("Worker stopped!");
    }

    /// <summary>
    /// Метод запуска получения данных
    /// </summary>
    /// <param name="receivedFilePath">Путь до файла сохранения полученных данных</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    private async Task StartGettingData(string receivedFilePath, CancellationToken stoppingToken)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                float receivedFileSizeGigaByte = 0;
                
                if (!File.Exists(receivedFilePath))
                {
                    using (FileStream fs = new FileStream(receivedFilePath, FileMode.CreateNew))
                    {
                        fs.Close();
                    }
                }

                while (receivedFileSizeGigaByte < _fileSizeThreshold)
                {
                    receivedFileSizeGigaByte = await GetReceivedFileSize(client, receivedFilePath, stoppingToken);
                    // await Task.Delay(5000, stoppingToken);
                }

                await SortAndSaveReceivedData(receivedFilePath, stoppingToken);

                // Пересоздание файла для получения данных
                File.Delete(receivedFilePath);
                // File.Create(receivedFilePath);
                _logger.LogInformation("File received_objects.txt was deleted and created again. Start again!");
            }
        }
        catch (Exception e)
        {
            _logger.LogInformation("Exception message: {message}", e.Message);
        }
    }


    /// <summary>
    /// Получение информации о размере файла
    /// </summary>
    /// <param name="client">HttpClient для отправки запроса</param>
    /// <param name="receivedFilePath">Путь до файла сохранения полученных данных</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    /// <returns>Размер файла в GB (гигабайт)</returns>
    private async Task<int> GetReceivedFileSize(HttpClient client, string receivedFilePath,
        CancellationToken stoppingToken)
    {
        int fileSizeGb = 0;

        if (stoppingToken.IsCancellationRequested) return fileSizeGb;

        try
        {
            fileSizeGb = (int)SizeConverterHelper.BytesToGigabytes(new FileInfo(receivedFilePath).Length);

            string responseBody = await GetObjects(client, stoppingToken);
            await SaveReceivedObjects(responseBody, receivedFilePath, stoppingToken);

            // Получим информацию о размере после того, как добавили новые данные
            long fileSize = new FileInfo(receivedFilePath).Length;

            _logger.LogInformation(
                "Received information save in file. Current time -> {time}. File size is {bytes} Bytes {kilobyte} KB -> {megabyte} MB -> {gigabyte} GB",
                DateTimeOffset.Now,
                fileSize,
                SizeConverterHelper.BytesToKilobytes(fileSize),
                SizeConverterHelper.BytesToMegabytes(fileSize),
                SizeConverterHelper.BytesToGigabytes(fileSize)
            );

            fileSizeGb = (int)SizeConverterHelper.BytesToGigabytes(fileSize);
        }
        catch (Exception e)
        {
            _logger.LogInformation("Exception message: {message}", e.Message);
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
    /// <param name="receivedFilePath">Путь до файла сохранения полученных данных</param>
    /// <param name="stoppingToken">Уведомление о прекращении операций</param>
    private async Task SaveReceivedObjects(string response, string receivedFilePath, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        GenerateObjectModel[]? objectModels = JsonSerializer.Deserialize<GenerateObjectModel[]>(response, options);

        using (StreamWriter writer = new StreamWriter(receivedFilePath, true, Encoding.UTF8))
        {
            if (objectModels != null)
                foreach (GenerateObjectModel item in objectModels)
                {
                    await writer.WriteLineAsync(item.Text);
                }

            writer.Close();
        }
    }

    private async Task SortAndSaveReceivedData(string receivedFilePath, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;

        List<string> lines = new List<string>();
        List<string> sortedLines = new List<string>();

        DateTimeOffset startTime;
        DateTimeOffset endTime;

        // Чтение сохраненных данных из received_objects.txt
        using (StreamReader reader = new StreamReader(receivedFilePath))
        {
            startTime = DateTimeOffset.Now;

            _logger.LogInformation("Start read received_objects.txt-> {startTime}", startTime);
            while (await reader.ReadLineAsync(stoppingToken) is { } line)
            {
                lines.Add(line);
            }

            _logger.LogInformation("End read received_objects.txt -> {endTime}", DateTimeOffset.Now);
            reader.Close();
        }

        _logger.LogInformation("Start sort info -> {startTime}", DateTimeOffset.Now);

        // Запуск сортировки по алфавиту
        foreach (char letter in _alphabet)
        {
            sortedLines.AddRange(
                lines.Where(e => e.ToLower().Contains($".{letter}"))
                    .OrderBy(o => o.Split(".").Last())
                    .Distinct()
                    .AsParallel()
                    .ToList()
            );
        }

        _logger.LogInformation("End sort info -> {endTime}", DateTimeOffset.Now);

        string pathWrite = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                @"Sorted values",
                $@"sorted_values_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.txt")
        );

        // Запись отсортированных элементов
        using (StreamWriter writer = new StreamWriter(pathWrite, true, Encoding.UTF8))
        {
            _logger.LogInformation("Start write to sorted file-> {startTime}", DateTimeOffset.Now);

            foreach (string line in sortedLines)
            {
                await writer.WriteLineAsync(line);
            }

            endTime = DateTimeOffset.Now;

            _logger.LogInformation("End write to sorted file -> {endTime}", endTime);
            writer.Close();
        }

        _logger.LogInformation("Operation done. Time delta -> {timeDelta}", endTime - startTime);
    }
}