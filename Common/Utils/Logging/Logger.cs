using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace Gaia.Common.Utils.Logging;
public static class Logger
{
    // Class name -> logging enabled/disabled
    private static Dictionary<string, bool> _registeredClasses = new Dictionary<string, bool>();
    private static bool _godotLoggingEnabled = false;

    public static void RegisterLogging<T>(this T obj, bool enabled)
    {
        _registeredClasses.TryAdd(typeof(T).Name, enabled);
    }

    public static void DisableLogging<T>(this T obj)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }
        _registeredClasses[typeof(T).Name] = false;
    }
    
    public static void EnableLogging<T>(this T obj)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }
        _registeredClasses[typeof(T).Name] = false;
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogBullshit<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=cyan]{fullMessage}[/color]");
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogSuccess<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=green]{fullMessage}[/color]");
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogInfo<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=blue]{fullMessage}[/color]");
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogInitialization<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=purple]{fullMessage}[/color]");
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogWarning<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=yellow]{fullMessage}[/color]");
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogError<T>(this T obj, string message)
    {
        if (!_registeredClasses.ContainsKey(typeof(T).Name))
        {
            return;
        }

        if (!_registeredClasses[typeof(T).Name])
        {
            return;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string fullMessage = $"{timestamp} {message}";

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write($"{timestamp} ");
        Console.WriteLine(message);
        Console.ResetColor();

        if (_godotLoggingEnabled)
        {
            GD.PrintRich($"[color=red]{fullMessage}[/color]");
        }
    }
}