// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using ToolSelection.Models;

namespace ToolSelection.Services;

public class EmbeddingService(HttpClient httpClient, string endpoint, string apiKey)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _endpoint = endpoint;
    private readonly string _apiKey = apiKey;

    public async Task<float[]> CreateEmbeddingsAsync(string input)
    {
        var requestBody = new EmbeddingRequest
        {
            Input = [input]
        };

        var json = JsonSerializer.Serialize(requestBody, SourceGenerationContext.Default.EmbeddingRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = content
        };

        if (TryGetBearerToken(_apiKey, out var bearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }
        else
        {
            request.Headers.Add("api-key", _apiKey);
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonSerializer.Deserialize(responseContent, SourceGenerationContext.Default.EmbeddingResponse);

        if (embeddingResponse?.Error != null)
        {
            throw new InvalidOperationException($"API error: {embeddingResponse.Error.Type} - {embeddingResponse.Error.Message}");
        }

        if (embeddingResponse?.Data == null || embeddingResponse.Data.Length == 0)
        {
            throw new InvalidOperationException($"No embedding data returned from API. Response: {responseContent}");
        }

        return embeddingResponse.Data[0].Embedding;
    }

    private static bool TryGetBearerToken(string apiKeyOrToken, out string? bearerToken)
    {
        bearerToken = null;
        if (string.IsNullOrWhiteSpace(apiKeyOrToken))
        {
            return false;
        }

        const string bearerPrefix = "Bearer ";
        if (apiKeyOrToken.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var tokenValue = apiKeyOrToken[bearerPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                return false;
            }

            bearerToken = tokenValue;
            return true;
        }

        var dotCount = 0;
        foreach (var character in apiKeyOrToken)
        {
            if (character == '.')
            {
                dotCount++;
            }
        }

        if (dotCount == 2)
        {
            bearerToken = apiKeyOrToken.Trim();
            return true;
        }

        return false;
    }
}
