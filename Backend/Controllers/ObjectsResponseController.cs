using Backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("/objects")]
public class ObjectsResponseController : ControllerBase
{
    private readonly ILogger<ObjectsResponseController> _logger;

    private readonly string _testFilePath =
        Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(), 
                @"testNotebookFile.txt"
                )
            );

    public ObjectsResponseController(ILogger<ObjectsResponseController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetObjectsResponse")]
    [ProducesResponseType(typeof(List<GenerateObjectModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetObjectsResponse([FromQuery] int minValue, [FromQuery] int maxValue)
    {
        return minValue switch
        {
            > 0 when maxValue > 0 && maxValue >= minValue => Ok(
                await Task.Run(async () =>
                    {
                        string[] lines = await Task.Run(() => System.IO.File.ReadAllLines(_testFilePath));
                        return Enumerable.Range(1, Random.Shared.Next(minValue, maxValue))
                            .Select(data => new GenerateObjectModel
                            {
                                Text = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{lines[Random.Shared.Next(0, lines.Length - 1)]}"
                            }).ToArray();
                    })
                ),
            _ => BadRequest("Не удалось получить данные! Проверьте входные параметры. Минимальное и максимальное значения не должны быть меньше или равны 0!")
        };
    }
}