using Microsoft.AspNetCore.Mvc;
using Memorizer.Self.Models;
using Memorizer.Self.Services;
using System.Text.Json;

namespace Memorizer.Self.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoriesController : ControllerBase
{
    private readonly MemoryService _memoryService;
    private readonly ILogger<MemoriesController> _logger;

    public MemoriesController(MemoryService memoryService, ILogger<MemoriesController> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Memory), StatusCodes.Status201Created)]
    public async Task<ActionResult<Memory>> CreateMemory([FromBody] CreateMemoryRequest request)
    {
        try
        {
            // Extract text from content
            string text;
            if (request.Content.RootElement.TryGetProperty("text", out var textElement))
            {
                text = textElement.GetString() ?? "";
            }
            else
            {
                text = request.Content.RootElement.GetRawText();
            }

            var memory = await _memoryService.StoreMemory(
                request.Type,
                request.Content,
                request.Source,
                text,
                request.Tags,
                request.Confidence,
                request.Title);

            return CreatedAtAction(nameof(GetMemory), new { id = memory.Id }, memory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating memory");
            return StatusCode(500, new { error = "Failed to create memory", details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Memory), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Memory>> GetMemory(Guid id)
    {
        var memory = await _memoryService.GetMemory(id);
        if (memory == null)
        {
            return NotFound(new { error = "Memory not found", id });
        }
        return Ok(memory);
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(List<Memory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Memory>>> SearchMemories([FromBody] SearchMemoryRequest request)
    {
        try
        {
            var memories = await _memoryService.SearchMemories(
                request.Query,
                request.Limit,
                request.FilterTags,
                request.MinSimilarity);

            return Ok(memories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching memories");
            return StatusCode(500, new { error = "Failed to search memories", details = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Memory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Memory>>> GetMemories([FromQuery] string ids)
    {
        try
        {
            var guidList = ids.Split(',')
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();

            if (guidList.Count == 0)
            {
                return BadRequest(new { error = "No valid IDs provided" });
            }

            var memories = await _memoryService.GetMemories(guidList);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting memories");
            return StatusCode(500, new { error = "Failed to get memories", details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMemory(Guid id)
    {
        var deleted = await _memoryService.DeleteMemory(id);
        if (!deleted)
        {
            return NotFound(new { error = "Memory not found", id });
        }
        return NoContent();
    }

    [HttpPost("relationships")]
    [ProducesResponseType(typeof(MemoryRelationship), StatusCodes.Status201Created)]
    public async Task<ActionResult<MemoryRelationship>> CreateRelationship(
        [FromBody] CreateRelationshipRequest request)
    {
        try
        {
            var relationship = await _memoryService.CreateRelationship(
                request.FromMemoryId,
                request.ToMemoryId,
                request.Type);

            return CreatedAtAction(nameof(GetMemory), new { id = relationship.Id }, relationship);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationship");
            return StatusCode(500, new { error = "Failed to create relationship", details = ex.Message });
        }
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, object>>> GetStatistics()
    {
        var stats = await _memoryService.GetStatistics();
        return Ok(stats);
    }
}
