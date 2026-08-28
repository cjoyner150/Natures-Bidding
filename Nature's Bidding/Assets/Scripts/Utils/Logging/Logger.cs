using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum LogSeverity
{
    None = 0,
    Error = 1,
    Warning = 2,
    Info = 3,
    Debug = 4,
    Verbose = 5
}

/// <summary>
/// Lightweight file logger. Every message is written into a separate file
/// per severity folder (Logs/Error, Logs/Warning, Logs/Info, Logs/Debug,
/// Logs/Verbose) — each folder's file contains every message AT OR ABOVE
/// its own severity in importance (i.e. the Verbose folder always has the
/// full log; the Error folder has only errors). Each folder independently
/// keeps only the N most recent session files.
///
/// CurrentLevel only controls what gets mirrored into the Unity Console —
/// it does NOT limit what gets written to disk. The full log at every
/// level is always available in its corresponding folder.
///
/// Usage:
///   GameLogger.CurrentLevel = LogSeverity.Info;   // console verbosity only
///   GameLogger.Log(LogSeverity.Debug, "message"); // tag = calling file name
/// </summary>
public static class GameLogger
{
    /// <summary>Controls ONLY what's mirrored to the Unity Console/Player.log. File output is unaffected.</summary>
    public static LogSeverity CurrentLevel = LogSeverity.Info;

    /// <summary>How many previous session log files to keep, per severity folder.</summary>
    public static int MaxSessionsToKeep = 5;

    private const string LogRootFolderName = "Logs";
    private const string FilePrefix = "session_log_";
    private const string FileExtension = ".log";

    // All levels that get their own folder/file. (None is excluded — it means "don't log".)
    private static readonly LogSeverity[] AllLevels =
    {
        LogSeverity.Error, LogSeverity.Warning, LogSeverity.Info, LogSeverity.Debug, LogSeverity.Verbose
    };

    private static readonly Dictionary<LogSeverity, StreamWriter> _writers = new Dictionary<LogSeverity, StreamWriter>();
    private static readonly object _lock = new object();
    private static string _logRootDirectory;
    private static bool _initialized;

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;

            _logRootDirectory = Path.Combine(Application.persistentDataPath, LogRootFolderName);

            string fileName = $"{FilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}{FileExtension}";
            string header = $"=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} | App v{Application.version} ===";

            foreach (var level in AllLevels)
            {
                string folder = Path.Combine(_logRootDirectory, level.ToString());
                try
                {
                    Directory.CreateDirectory(folder);
                    PruneOldSessions(folder);

                    string fullPath = Path.Combine(folder, fileName);
                    var writer = new StreamWriter(fullPath, append: false, Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine(header);
                    _writers[level] = writer;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameLogger] Failed to open log file for level {level} in {folder}: {e}");
                }
            }

            Application.quitting += Shutdown;
        }
    }

    /// <summary>Logs a message. Tag is automatically the calling file's name.</summary>
    public static void Log(LogSeverity level, string message,
        [CallerFilePath] string callerFilePath = "")
    {
        if (!_initialized) Initialize();
        if (level == LogSeverity.None) return;

        string tag = TagFromPath(callerFilePath);
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{tag}] {message}";

        WriteToFiles(level, line);

        // Console mirroring is gated by CurrentLevel, independent of file output.
        if (level <= CurrentLevel)
        {
            switch (level)
            {
                case LogSeverity.Error: Debug.LogError(line); break;
                case LogSeverity.Warning: Debug.LogWarning(line); break;
                default: Debug.Log(line); break;
            }
        }
    }

    public static void LogException(LogSeverity level, string message, Exception e,
        [CallerFilePath] string callerFilePath = "")
    {
        Log(level, $"{message}\n{e.GetType().Name}: {e.Message}\n{e.StackTrace}", callerFilePath);
    }

    private static string TagFromPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "Unknown";
        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Writes the line to every folder whose severity is >= the message's severity
    /// (i.e. every folder "verbose enough" to include this message). Lower enum
    /// values are more severe/important, so e.g. an Error (1) message goes into
    /// Error, Warning, Info, Debug, AND Verbose folders; a Verbose (5) message
    /// only goes into the Verbose folder.
    /// </summary>
    private static void WriteToFiles(LogSeverity messageLevel, string line)
    {
        lock (_lock)
        {
            foreach (var kvp in _writers)
            {
                LogSeverity folderLevel = kvp.Key;
                if (messageLevel > folderLevel) continue; // this folder isn't verbose enough for this message

                try { kvp.Value?.WriteLine(line); }
                catch { /* never let logging crash the game */ }
            }
        }
    }

    private static void PruneOldSessions(string folder)
    {
        try
        {
            var files = new DirectoryInfo(folder)
                .GetFiles($"{FilePrefix}*{FileExtension}")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            for (int i = MaxSessionsToKeep - 1; i < files.Count; i++)
            {
                try { files[i].Delete(); }
                catch (Exception e) { Debug.LogWarning($"[GameLogger] Could not delete old log {files[i].Name}: {e.Message}"); }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] Prune failed for {folder}: {e.Message}");
        }
    }

    public static void Shutdown()
    {
        lock (_lock)
        {
            string footer = $"=== Session ended {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";

            foreach (var kvp in _writers)
            {
                try
                {
                    kvp.Value?.WriteLine(footer);
                    kvp.Value?.Flush();
                    kvp.Value?.Dispose();
                }
                catch { /* ignore */ }
            }
            _writers.Clear();
            _initialized = false;
        }

        Application.quitting -= Shutdown;
    }
}