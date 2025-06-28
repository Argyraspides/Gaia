using System;
using Godot;

namespace Gaia.Common.Utils.Logging;

public static partial class Logger
{
    private static bool m_infoLoggingEnabled = true;
    private static bool m_warningLoggingEnabled = true;
    private static bool m_initializationLoggingEnabled = true;
    private static bool m_errorLoggingEnabled = false;
    private static bool m_successLoggingEnabled = true;
    private static bool m_bullshitLoggingEnabled = false;
    private static bool m_godotLoggingEnabled = false;
    
    public static void LogBullshit(string message)
    {
        if (!m_bullshitLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=cyan]{fullMessage}[/color]");
        }
    }

    public static void LogSuccess(string message)
    {
        if (!m_successLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=green]{fullMessage}[/color]");
        }
    }

    public static void LogInfo(string message)
    {
        if (!m_infoLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=blue]{fullMessage}[/color]");
        }
    }

    public static void LogInitialization(string message)
    {
        if (!m_initializationLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=purple]{fullMessage}[/color]");
        }
    }

    public static void LogWarning(string message)
    {
        if (!m_warningLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=yellow]{fullMessage}[/color]");
        }
    }

    public static void LogError(string message)
    {
        if (!m_errorLoggingEnabled) return;
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";
        
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();
        
        if (m_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=red]{fullMessage}[/color]");
        }
    }
}