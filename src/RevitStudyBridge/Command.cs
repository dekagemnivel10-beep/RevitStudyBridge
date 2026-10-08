using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitStudyBridge;

[Transaction(TransactionMode.ReadOnly)]
public sealed class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument?.Document;
        if (document is null)
        {
            message = "NO_ACTIVE_DOCUMENT";
            return Result.Cancelled;
        }

        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitStudyBridge");
        Request? request = null;
        bool claimed = false;
        try
        {
            AssertDirectoryTree(root);
            string pending = Path.Combine(root, "request.json");
            if ((File.GetAttributes(pending) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("REQUEST_LINK");
            byte[] bytes;
            using (var input = new FileStream(pending, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length < 1 || input.Length > Protocol.MaximumRequestBytes)
                    throw new ArgumentException("REQUEST_SIZE");
                bytes = new byte[checked((int)input.Length)];
                input.ReadExactly(bytes);
            }
            request = Protocol.Parse(bytes);

            var choice = TaskDialog.Show("Revit Study Bridge",
                "Read aggregate element counts from the active document and write a local response? " +
                "No model changes, model names, element IDs or geometry will be exported.",
                TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel);
            if (choice != TaskDialogResult.Ok) return Result.Cancelled;

            string processed = Path.Combine(root, "processed");
            string responses = Path.Combine(root, "responses");
            Directory.CreateDirectory(processed);
            Directory.CreateDirectory(responses);
            AssertDirectoryTree(processed);
            AssertDirectoryTree(responses);
            string response = Path.Combine(responses, request.Id.ToString("D") + ".json");
            if (File.Exists(response)) throw new IOException("RESPONSE_EXISTS");

            // Claim once. Failed claims remain visible and are never retried automatically.
            File.Move(pending, Path.Combine(processed, request.Id.ToString("D") + ".request.json"), false);
            claimed = true;
            var watch = Stopwatch.StartNew();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var categories = new Dictionary<long, string>
            {
                [(long)BuiltInCategory.OST_Walls] = "walls",
                [(long)BuiltInCategory.OST_Doors] = "doors",
                [(long)BuiltInCategory.OST_Windows] = "windows",
                [(long)BuiltInCategory.OST_Floors] = "floors",
                [(long)BuiltInCategory.OST_Rooms] = "rooms"
            };
            foreach (string name in categories.Values) counts[name] = 0;
            int inspected = 0;
            using var collector = new FilteredElementCollector(document).WhereElementIsNotElementType();
            foreach (Element element in collector)
            {
                if (++inspected > 100000 || watch.Elapsed > TimeSpan.FromSeconds(5))
                    throw new TimeoutException("SUMMARY_LIMIT");
                if (element.Category is { } category && categories.TryGetValue(category.Id.Value, out string? name))
                    counts[name]++;
            }
            byte[] output = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = 1, id = request.Id.ToString("D"), operation = request.Operation,
                status = "succeeded", modelModified = false, inspectedElements = inspected, counts
            });
            WriteNewResponse(response, output);
            return Result.Succeeded;
        }
        catch
        {
            // Do not leak API exceptions, model paths or document information.
            message = claimed ? "CLAIMED_REQUEST_FAILED_NO_AUTOMATIC_RETRY" : "REQUEST_NOT_EXECUTED";
            if (claimed && request is not null)
            {
                try
                {
                    string response = Path.Combine(root, "responses", request.Id.ToString("D") + ".json");
                    WriteNewResponse(response, JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        schema = 1, id = request.Id.ToString("D"), operation = request.Operation,
                        status = "failed", code = "SUMMARY_FAILED", modelModified = false
                    }));
                }
                catch { message = "CLAIMED_REQUEST_FAILED_RESPONSE_NOT_CONFIRMED"; }
            }
            return Result.Failed;
        }
    }

    private static void AssertDirectoryTree(string path)
    {
        for (DirectoryInfo? directory = new(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("DIRECTORY_BOUNDARY");
    }

    private static void WriteNewResponse(string path, byte[] bytes)
    {
        if (bytes.Length > 4096) throw new IOException("RESPONSE_SIZE");
        AssertDirectoryTree(Path.GetDirectoryName(path)!);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(true);
    }
}
