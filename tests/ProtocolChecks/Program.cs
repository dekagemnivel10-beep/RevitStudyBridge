using System.Text;
using RevitStudyBridge;

const string Valid = "{\"schema\":1,\"id\":\"80d9ffec-ad5a-4f40-95cb-5d248431c51d\",\"operation\":\"summarize_active_document\"}";
if (Protocol.Parse(Encoding.UTF8.GetBytes(Valid)).Operation != Protocol.SupportedOperation)
    throw new Exception("VALID_FAILED");
string[] invalid =
{
    "", "[]", "{}", "{",
    Valid.Replace("\"schema\":1", "\"schema\":2"),
    Valid.Replace("\"schema\":1", "\"schema\":1,\"schema\":1"),
    Valid.Replace("\"schema\":1", "\"schema\":1,\"path\":\"example\""),
    Valid.Replace("summarize_active_document", "save_model"),
    Valid.Replace("80d9ffec-ad5a-4f40-95cb-5d248431c51d", "00000000-0000-0000-0000-000000000000"),
    new string(' ', Protocol.MaximumRequestBytes + 1)
};
foreach (string text in invalid)
{
    bool rejected = false;
    try { Protocol.Parse(Encoding.UTF8.GetBytes(text)); }
    catch (Exception e) when (e is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
    { rejected = true; }
    if (!rejected) throw new Exception("INVALID_ACCEPTED");
}
Console.WriteLine("Protocol checks: 11 passed. Revit execution was not tested.");
