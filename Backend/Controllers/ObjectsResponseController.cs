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
    public async Task<IActionResult> GetObjectsResponse([FromQuery] int min, [FromQuery] int max)
    {
        return min switch
        {
            > 0 when max > 0 && max >= min => Ok(
                await Task.Run(async () =>
                    {
                        string[] lines = await Task.Run(() => System.IO.File.ReadAllLines(_testFilePath));
                        return Enumerable.Range(1, Random.Shared.Next(min, max))
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