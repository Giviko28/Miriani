using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Integration smoke test: relays a prompt through the Python AI service to the local LLM.
/// Proves the React -> .NET -> AI -> Ollama chain. Anonymous for now; real AI endpoints
/// (authenticated, RAG-backed) arrive in later milestones.
/// </summary>
[ApiController]
[Route("api/smoke")]
public class SmokeController(IHttpClientFactory factory) : ControllerBase
{
    public record PingRequest(string? Prompt);
    public record PingResult(string Model, string Reply, double Elapsed_Seconds);

    [AllowAnonymous]
    [HttpPost("ping-llm")]
    public async Task<IActionResult> PingLlm(PingRequest req, CancellationToken ct)
    {
        var client = factory.CreateClient("ai");
        var prompt = string.IsNullOrWhiteSpace(req.Prompt)
            ? "Say hello in one short sentence."
            : req.Prompt;

        try
        {
            var response = await client.PostAsJsonAsync("/ping-llm", new { prompt }, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return Problem($"AI service returned {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<PingResult>(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Problem($"Could not reach AI service: {ex.Message}");
        }
    }
}
