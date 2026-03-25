using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MulhollandRealEstate.API.Models;

namespace MulhollandRealEstate.API.Services;

public class TriageService : ITriageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TriageService> _logger;

    public TriageService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TriageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TriageResult> TriageAsync(MaintenanceRequest ticket, CancellationToken cancellationToken = default)
    {
        var webhookUrl = _configuration["N8n:TriageWebhookUrl"];
        var useWebhook = _configuration.GetValue("N8n:Enabled", true);

        if (useWebhook && !string.IsNullOrWhiteSpace(webhookUrl))
        {
            try
            {
                return await TriageWithN8nAsync(ticket, webhookUrl!, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "n8n triage webhook failed; falling back to heuristic.");
            }
        }

        return HeuristicTriage(ticket);
    }

    private async Task<TriageResult> TriageWithN8nAsync(MaintenanceRequest ticket, string webhookUrl, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var dto = new N8nTriageRequestDto
        {
            RequestNumber = ticket.RequestNumber,
            PropertyId = ticket.PropertyId,
            UnitNumber = ticket.UnitNumber,
            BuildingType = ticket.BuildingType,
            TenantTenureMonths = ticket.TenantTenureMonths,
            SubmissionChannel = ticket.SubmissionChannel,
            RequestTimestamp = ticket.RequestTimestamp,
            RequestText = ticket.RequestText,
            HasImage = ticket.HasImage,
            ImageType = ticket.ImageType,
            ImageSeverityHint = ticket.ImageSeverityHint,
            ImageUrlOrCount = ticket.ImageUrlOrCount,
            PriorRequestsLast6Mo = ticket.PriorRequestsLast6Mo,
            ActualCategory = ticket.ActualCategory,
            ActualUrgency = ticket.ActualUrgency
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var req = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddN8nCredentials(req.Headers);

        using var resp = await client.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("n8n webhook HTTP {Status}: {Body}", (int)resp.StatusCode, respBody);
            throw new InvalidOperationException("n8n webhook returned an error status.");
        }

        var parsed = TryParseN8nResponse(respBody);
        if (parsed is null)
            throw new InvalidOperationException("Could not parse n8n triage JSON.");

        return new TriageResult
        {
            PredictedCategory = NormalizeCategory(parsed.PredictedCategory),
            PredictedUrgency = NormalizeUrgency(parsed.PredictedUrgency),
            Confidence = Clamp01(parsed.Confidence),
            Tags = parsed.Tags ?? [],
            RiskNotes = parsed.RiskNotes ?? "",
            Source = "n8n"
        };
    }

    /// <summary>Parses raw body: either a direct object or common n8n wrappers (e.g. array of items).</summary>
    private static N8nTriageResponseDto? TryParseN8nResponse(string respBody)
    {
        using var doc = JsonDocument.Parse(respBody);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            root = root[0];

        if (root.TryGetProperty("json", out var jsonProp) && jsonProp.ValueKind == JsonValueKind.Object)
            root = jsonProp;

        if (root.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.Object)
            root = bodyProp;

        return JsonSerializer.Deserialize<N8nTriageResponseDto>(root.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private void AddN8nCredentials(HttpRequestHeaders headers)
    {
        var clientId =
            _configuration["N8n:ClientId"] ??
            _configuration["N8n:ClientID"] ??
            _configuration["ClientId"] ??
            _configuration["ClientID"];
        var clientSecret =
            _configuration["N8n:ClientSecret"] ??
            _configuration["ClientSecret"];
        var clientIdHeader = _configuration["N8n:ClientIdHeader"] ?? "X-Client-Id";
        var clientSecretHeader = _configuration["N8n:ClientSecretHeader"] ?? "X-Client-Secret";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return;

        headers.TryAddWithoutValidation(clientIdHeader, clientId);
        headers.TryAddWithoutValidation(clientSecretHeader, clientSecret);
    }

    private static TriageResult HeuristicTriage(MaintenanceRequest t)
    {
        var text = (t.RequestText + " " + t.ImageType + " " + t.ImageSeverityHint).ToLowerInvariant();
        var urgency = "Routine";
        var category = "Other";

        if (text.Contains("gas smell") || text.Contains("spark") || text.Contains("flooding") || text.Contains("power out"))
            urgency = "Emergency";
        else if (text.Contains("gas ") || text.Contains("electrocut"))
            urgency = "Emergency";
        else if (text.Contains("no heat") || text.Contains("overflowing") || text.Contains("leaking") && text.Contains("soaked"))
            urgency = "Emergency";
        else if (text.Contains("drip") || text.Contains("flicker") || text.Contains("peeling") || text.Contains("hinge"))
            urgency = "Routine";
        else if (text.Contains("not working") || text.Contains("leak") || text.Contains("crack") || text.Contains("stopped"))
            urgency = "Urgent";

        if (text.Contains("water") || text.Contains("toilet") || text.Contains("sink") || text.Contains("drain") || text.Contains("plumb"))
            category = "Plumbing";
        else if (text.Contains("light") || text.Contains("power") || text.Contains("outlet") || text.Contains("spark") || text.Contains("detector") || text.Contains("fan"))
            category = "Electrical";
        else if (text.Contains("heat") || text.Contains("ac") || text.Contains("hvac") || text.Contains("vent") || text.Contains("thermostat") || text.Contains("heater"))
            category = "HVAC";
        else if (text.Contains("dishwasher") || text.Contains("oven") || text.Contains("refrigerator") || text.Contains("disposal") || text.Contains("washing machine"))
            category = "Appliance";
        else if (text.Contains("ceiling") || text.Contains("wall") || text.Contains("door") || text.Contains("window") || text.Contains("balcony") || text.Contains("crack") || text.Contains("structural"))
            category = "Structural";

        if (t.ImageSeverityHint.Equals("High", StringComparison.OrdinalIgnoreCase) && urgency == "Routine")
            urgency = "Urgent";
        if (t.PriorRequestsLast6Mo >= 3 && urgency == "Routine")
            urgency = "Urgent";

        var confidence = 0.62;
        if (category == "Other")
            confidence = 0.48;
        if (urgency == "Emergency")
            confidence = Math.Min(0.72, confidence + 0.05);

        var tags = new List<string>();
        if (text.Contains("water") || text.Contains("leak")) tags.Add("water");
        if (urgency == "Emergency") tags.Add("safety");
        if (t.HasImage) tags.Add("photo");

        return new TriageResult
        {
            PredictedCategory = category,
            PredictedUrgency = urgency,
            Confidence = confidence,
            Tags = tags.ToArray(),
            RiskNotes = urgency == "Emergency" ? "Possible habitability or safety impact; verify quickly." : "Monitor if tenant reports worsening.",
            Source = "heuristic"
        };
    }

    private static string NormalizeCategory(string? value)
    {
        var v = value?.Trim() ?? "";
        var allowed = new[] { "Plumbing", "Electrical", "HVAC", "Appliance", "Structural", "Other" };
        foreach (var a in allowed)
            if (a.Equals(v, StringComparison.OrdinalIgnoreCase))
                return a;
        return "Other";
    }

    private static string NormalizeUrgency(string? value)
    {
        var v = value?.Trim() ?? "";
        foreach (var a in new[] { "Routine", "Urgent", "Emergency" })
            if (a.Equals(v, StringComparison.OrdinalIgnoreCase))
                return a;
        return "Routine";
    }

    private static double Clamp01(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return 0.5;
        if (x < 0) return 0;
        if (x > 1) return 1;
        return x;
    }
}
