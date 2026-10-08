using System.Text.Json;

namespace RevitStudyBridge;

public sealed record Request(Guid Id, string Operation);

public static class Protocol
{
    public const int MaximumRequestBytes = 4096;
    public const string SupportedOperation = "summarize_active_document";

    public static Request Parse(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumRequestBytes)
            throw new ArgumentException("REQUEST_SIZE");

        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("REQUEST_SCHEMA");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (!names.Add(property.Name) || property.Name is not ("schema" or "id" or "operation"))
                throw new ArgumentException("REQUEST_FIELDS");
        }
        if (names.Count != 3 || !json.RootElement.GetProperty("schema").TryGetInt32(out var schema) || schema != 1)
            throw new ArgumentException("REQUEST_SCHEMA");

        var idValue = json.RootElement.GetProperty("id");
        var operationValue = json.RootElement.GetProperty("operation");
        if (idValue.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(idValue.GetString(), "D", out var id) || id == Guid.Empty ||
            operationValue.ValueKind != JsonValueKind.String ||
            operationValue.GetString() != SupportedOperation)
            throw new ArgumentException("REQUEST_OPERATION");

        return new Request(id, SupportedOperation);
    }
}
