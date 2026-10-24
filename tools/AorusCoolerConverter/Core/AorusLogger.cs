using System;
using System.IO;
using System.Text;

namespace AorusCoolerConverter.Core;

public static class AorusLogger
{
    private static readonly object _lock = new();
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aorus_flash_debug.log");

    public static event Action<string>? OnLogMessage;

    public static string GetLogPath() => LogFilePath;

    public static void Log(string tag, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string formatted = $"[{timestamp}] [{tag,-7}] {message}";

        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, formatted + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging should never throw
        }

        try
        {
            OnLogMessage?.Invoke(formatted);
        }
        catch
        {
        }
    }

    public static void LogHex(string tag, string description, byte[] data, int length = -1)
    {
        if (data == null || data.Length == 0)
        {
            Log(tag, $"{description}: (0 bytes / vazio)");
            return;
        }

        int count = length > 0 ? Math.Min(length, data.Length) : data.Length;
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append(data[i].ToString("X2"));
            if (i < count - 1) sb.Append(' ');
            if (i > 0 && (i + 1) % 16 == 0 && i < count - 1) sb.Append(" | ");
        }

        Log(tag, $"{description} ({count} bytes): [ {sb} ]");
    }

    public static void Clear()
    {
        try
        {
            lock (_lock)
            {
                File.WriteAllText(LogFilePath, $"=== AORUS Liquid Cooler Flash Debug Log Iniciado em {DateTime.Now} ===" + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}
