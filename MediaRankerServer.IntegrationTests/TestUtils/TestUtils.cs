using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MediaRankerServer.IntegrationTests.Utils;

public static class TestUtils
{
    public static void AssertSuccessResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var problem = response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>().Result;
        
        var type = problem?["type"]?.ToString() ?? "Unknown";
        var detail = problem?["detail"]?.ToString();
        var errorId = problem?["errorId"]?.ToString();
        
        throw new Exception($"[{type}] Request failed. Status: {response.StatusCode}, ErrorId: {errorId}, Detail: {detail}");
    }
}
