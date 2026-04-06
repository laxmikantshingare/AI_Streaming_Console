using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Streaming Demo — streams responses token-by-token, just like ChatGPT/Claude.
/// FREE tier: 1500 requests/day at no cost.
/// Get your free API key at: https://aistudio.google.com/app/apikey
/// Requires: .NET 8+
/// </summary>
class Program
{
    // ── Config ────────────────────────────────────────────────────────────────
    private const string Model = "gemini-2.5-flash";   // fast & free

    // Paste your free API key here (from https://aistudio.google.com/app/apikey)
    private static readonly string ApiKey = "SetYourAPIKeyHere";
        //Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "YOUR_API_KEY_HERE";

    private static string ApiUrl =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:streamGenerateContent?alt=sse&key={ApiKey}";
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly HttpClient Http = new();

    // Conversation history (multi-turn)
    private static readonly List<object> History = new();

    static async Task Main()
    {
        Console.Title = "MyConsoleAI";
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        if (ApiKey == "YOUR_API_KEY_HERE")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("⚠  No API key found!");
            Console.WriteLine("   1. Go to https://aistudio.google.com/app/apikey");
            Console.WriteLine("   2. Click 'Create API Key' (free, no credit card)");
            Console.WriteLine("   3. Paste it in Program.cs replacing YOUR_API_KEY_HERE\n");
            Console.ResetColor();
            Console.Beep();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Ready! Type your message and press Enter.");
        Console.WriteLine("  Commands: 'clear' = new conversation  |  'quit' = exit\n");
        Console.ResetColor();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You: ");
            Console.ResetColor();

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

            if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                History.Clear();
                Console.WriteLine("  [Conversation cleared]\n");
                continue;
            }

            // Add user turn to history
            History.Add(new
            {
                role = "user",
                parts = new[] { new { text = input } }
            });

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("My AI: ");
            Console.ResetColor();

            var reply = await StreamResponseAsync();

            // Add assistant reply to history for multi-turn
            if (!string.IsNullOrEmpty(reply))
            {
                History.Add(new
                {
                    role = "model",
                    parts = new[] { new { text = reply } }
                });
            }

            Console.WriteLine("\n");
            Console.Beep();
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Goodbye!");
        Console.ResetColor();
    }

    static async Task<string> StreamResponseAsync()
    {
        var requestBody = new { contents = History };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await Http.PostAsync(ApiUrl, content);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Network error: {ex.Message}]");
            Console.ResetColor();
            return string.Empty;
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[API error {(int)response.StatusCode}: {err}]");
            Console.ResetColor();
            return string.Empty;
        }

        var fullReply = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            using (doc)
            {
                // Gemini SSE path: candidates[0].content.parts[0].text
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var contentEl) &&
                        contentEl.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textEl))
                    {
                        var chunk = textEl.GetString() ?? string.Empty;
                        Console.Write(chunk);        // ← live token output
                        fullReply.Append(chunk);
                    }
                }
            }
        }

        return fullReply.ToString();
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("""
        ╔═════════════════════════════════════════════════════════════════════════════════════╗
        ║                       Welcome to my Console AI                                      ║
        ║                       AI Demo using C#                                              ║                         
        ║                       Token-by-token output like Multi Turn Chat System             ║
        ║                       ASK ME ANYTHING.. :)                                          ║
        ║                       -- LS.                                                        ║
        ╚═════════════════════════════════════════════════════════════════════════════════════╝
        """);
        Console.ResetColor();
    }
}
