using System;

namespace Gaia.Common.Utils.Logging;

public static partial class Logging
{
    
    private static bool m_infoLoggingEnabled = true;
    private static bool m_warningLoggingEnabled = true;
    private static bool m_initializationLoggingEnabled = true;
    private static bool m_errorLoggingEnabled = true;
    private static bool m_successLoggingEnabled = true;
    private static bool m_bullshitLoggingEnabled = false;
    
    public static void LogBullshit(string message)
    {
        if (!m_bullshitLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogSuccess(string message)
    {
        if(!m_successLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogInfo(string message)
    {
        if(!m_infoLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogInitialization(string message)
    {
        if(!m_initializationLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogWarning(string message)
    {
        if(!m_warningLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogError(string message)
    {
        if(!m_errorLoggingEnabled) return;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} ");
        Console.WriteLine(message);
        Console.ResetColor();
    }
}