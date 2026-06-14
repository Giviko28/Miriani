// .NET 10 API — smoke-test stage.
// Acts as the gateway: the React app calls this service, and this service calls the
// Python AI service (which calls Ollama). Auth, roles, EF Core, and real business
// endpoints are added in Milestone 2. For now it just proves the React -> .NET -> AI hop.

const string FrontendCors = "frontend";

var builder = WebApplication.CreateBuilder(args);

// AI service base URL (the Python FastAPI app). Overridable via config / env.
var aiServiceUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8001";

builder.Services.AddHttpClient("ai", client =>
{
    client.BaseAddress = new Uri(aiServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(FrontendCors);

app.MapGet("/", () => "BPA API — ok");

// Smoke test: forward a prompt through the AI service to the local LLM.
app.MapPost("/api/smoke/ping-llm", async (PingRequest req, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ai");
    var prompt = string.IsNullOrWhiteSpace(req.Prompt)
        ? "Say hello in one short sentence."
        : req.Prompt;

    try
    {
        var response = await client.PostAsJsonAsync("/ping-llm", new { prompt });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return Results.Problem($"AI service returned {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<PingResult>();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not reach AI service at {aiServiceUrl}: {ex.Message}");
    }
});

app.Run();

record PingRequest(string? Prompt);
record PingResult(string Model, string Reply, double Elapsed_Seconds);
