using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class UnityEditorWebServer
{
    private static TcpListener _tcpListener;
    private static Thread _serverThread;
    private static bool _isRunning;
    private static string _authToken;
    private static int _boundPort;

    private static readonly string PortFilePath = Path.Combine(Application.dataPath, "../.unity-hermes-port");
    private static readonly string TokenFilePath = Path.Combine(Application.dataPath, "../.unity-hermes-token");

    private static readonly List<CompileError> _compiledErrorCache = new List<CompileError>();
    private static readonly object _lockObject = new object();

    private static bool _isPlayingCache;
    private static string _unityVersionCache;

    // --- Log Watcher 변수 추가 ---
    private static string _consoleLogPath;
    private static FileSystemWatcher _logWatcher;
    private static long _lastLogReadPosition = 0;
    private static readonly object _logReadLock = new object(); // 파일 동시 접근 방지용 락

    // 정규식: Native 에러 로그 포맷 추출용 (예: "Assets/Script.cs(10,5): error CS1002: ; expected")
    private static readonly Regex _errorRegex = new Regex(
        @"^(.*?)\((\d+),(\d+)\):\s+(error\s+CS\d+):\s+(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Static constructor - runs automatically when class is first accessed
    static UnityEditorWebServer()
    {
        // Unity API 중 일부는 정적 생성자에서 호출 시 메인 스레드가 아닐 수 있어 안전하게 지연 호출
        EditorApplication.delayCall += Initialize;
    }

    private static void Initialize()
    {
        Debug.Log("[Hermes-Bridge] Initializing Web Server & Log Watcher...");

        // 메인 스레드에서 안전하게 로그 경로 확보
        _consoleLogPath = Application.consoleLogPath;

        AssemblyReloadEvents.beforeAssemblyReload -= StopServer;
        AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        EditorApplication.quitting -= StopServer;
        EditorApplication.quitting += StopServer;

        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;

        Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        Application.logMessageReceivedThreaded += OnLogMessageReceived;
        Application.logMessageReceived -= OnLogMessageReceivedMainThread;
        Application.logMessageReceived += OnLogMessageReceivedMainThread;

        _unityVersionCache = Application.unityVersion;
        EditorApplication.update -= UpdateMainThreadCache;
        EditorApplication.update += UpdateMainThreadCache;

        _authToken = Guid.NewGuid().ToString("N").Substring(0, 16);
        Debug.Log("[Hermes-Bridge] EVENT SUBSCRIPTIONS COMPLETE");

        StartLogWatcher(); // Log Watcher 시작
        StartServer();
        Debug.Log("[Hermes-Bridge] Initialization COMPLETE");
    }

    private static void UpdateMainThreadCache()
    {
        _isPlayingCache = EditorApplication.isPlaying;
    }

    #region 컴파일 콜백 & C# 로그 인터셉트 (기존 로직 유지)

    private static void OnAssemblyCompilationFinished(string assemblyName, CompilerMessage[] messages)
    {
        Debug.Log($"[Hermes-Bridge] OnAssemblyCompilationFinished CALLED for: {assemblyName}, messages: {messages?.Length ?? 0}");
        CacheCompilerMessages(messages);
    }

    private static void OnCompilationFinished(object compilationContext)
    {
        Debug.Log("[Hermes-Bridge] Compilation Pipeline FULLY Finished.");
        ParseNewLogLines(); // 전체 컴파일 완료 시 누락된 로그가 없는지 한 번 더 긁어오기
    }

    private static void CacheCompilerMessages(CompilerMessage[] messages)
    {
        if (messages == null || messages.Length == 0) return;

        var errors = new List<CompileError>();
        foreach (var msg in messages)
        {
            if (msg.type == CompilerMessageType.Error)
            {
                errors.Add(new CompileError
                {
                    file = msg.file.Replace("\\", "/"),
                    line = msg.line,
                    column = msg.column,
                    message = msg.message.Trim(),
                    type = "error"
                });
            }
        }

        lock (_lockObject)
        {
            foreach (var err in errors)
            {
                bool exists = _compiledErrorCache.Exists(e => e.file == err.file && e.line == err.line && e.message == err.message);
                if (!exists) _compiledErrorCache.Add(err);
            }
        }
        Debug.Log($"[Hermes-Bridge] Cache size after callback: {_compiledErrorCache.Count}");
    }

    private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception && logString.Contains("error CS"))
            lock (_lockObject) { ParseAndCacheCompilerError(logString); }
    }

    private static void OnLogMessageReceivedMainThread(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception && logString.Contains("error CS"))
            lock (_lockObject) { ParseAndCacheCompilerError(logString); }
    }

    private static void ParseAndCacheCompilerError(string log)
    {
        try
        {
            int errorCsIdx = log.IndexOf("): error CS");
            if (errorCsIdx == -1) return;

            int openParenIdx = log.LastIndexOf('(', errorCsIdx);
            if (openParenIdx == -1) return;

            string file = log.Substring(0, openParenIdx).Trim().Replace("\\", "/");
            string lineColStr = log.Substring(openParenIdx + 1, errorCsIdx - openParenIdx - 1);
            string[] lineColParts = lineColStr.Split(',');

            int line = 0, column = 0;
            if (lineColParts.Length > 0) int.TryParse(lineColParts[0], out line);
            if (lineColParts.Length > 1) int.TryParse(lineColParts[1], out column);

            int colonIdx = log.IndexOf(':', errorCsIdx + 2);
            string cleanMessage = colonIdx != -1 ? log.Substring(colonIdx + 1).Trim() : log.Substring(errorCsIdx + 2).Trim();

            foreach (var existing in _compiledErrorCache)
            {
                if (existing.file == file && existing.line == line && existing.message == cleanMessage) return;
            }

            _compiledErrorCache.Add(new CompileError
            {
                file = file,
                line = line,
                column = column,
                message = log.Trim(),
                type = "error"
            });
        }
        catch { }
    }

    #endregion

    #region Editor.log Watcher (Unity 6 Native 에러 추출용)

    private static void StartLogWatcher()
    {
        if (string.IsNullOrEmpty(_consoleLogPath) || !File.Exists(_consoleLogPath))
        {
            Debug.LogWarning($"[Hermes-Bridge] Editor.log file not found at: {_consoleLogPath}");
            return;
        }

        try
        {
            // 현재 파일 크기를 초기 오프셋으로 설정하여 과거 로그는 스킵
            _lastLogReadPosition = new FileInfo(_consoleLogPath).Length;

            string directory = Path.GetDirectoryName(_consoleLogPath);
            string fileName = Path.GetFileName(_consoleLogPath);

            _logWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _logWatcher.Changed += (s, e) => ParseNewLogLines();
            Debug.Log($"[Hermes-Bridge] Started watching Editor.log at: {_consoleLogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Hermes-Bridge] Failed to start log watcher: {e.Message}");
        }
    }

    private static void ParseNewLogLines()
    {
        // TCP 스레드와 Watcher 이벤트 스레드가 동시에 접근하는 것을 방지
        lock (_logReadLock)
        {
            try
            {
                if (!File.Exists(_consoleLogPath)) return;

                // FileAccess.Read와 FileShare.ReadWrite가 핵심입니다.
                // Unity가 파일에 Write 하고 있어도 강제로 읽기 전용으로 열어 스트림을 가져옵니다.
                using (FileStream fs = new FileStream(_consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous))
                {
                    if (fs.Length < _lastLogReadPosition) _lastLogReadPosition = 0; // 유니티 재시작 등 파일이 작아졌을 때 대비
                    if (fs.Length == _lastLogReadPosition) return;

                    fs.Seek(_lastLogReadPosition, SeekOrigin.Begin);

                    using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains("error CS")) ExtractErrorFromLogLine(line.Trim());
                        }
                    }
                    _lastLogReadPosition = fs.Length;
                }
            }
            catch (Exception) { /* 공유 위반(Sharing Violation) 등 일시적 파일 접근 에러는 무시 */ }
        }
    }

    private static void ExtractErrorFromLogLine(string logLine)
    {
        Match match = _errorRegex.Match(logLine);
        if (match.Success)
        {
            string file = match.Groups[1].Value.Replace("\\", "/").Trim();
            int.TryParse(match.Groups[2].Value, out int line);
            int.TryParse(match.Groups[3].Value, out int column);
            string errCode = match.Groups[4].Value.Trim();
            string msg = match.Groups[5].Value.Trim();

            lock (_lockObject)
            {
                bool exists = _compiledErrorCache.Exists(e => e.file == file && e.line == line && e.message.Contains(errCode));
                if (!exists)
                {
                    _compiledErrorCache.Add(new CompileError
                    {
                        file = file,
                        line = line,
                        column = column,
                        message = $"{errCode}: {msg}",
                        type = "error"
                    });
                    Debug.Log($"[Hermes-Bridge] Native error captured! {file}:{line}");
                }
            }
        }
    }

    #endregion

    #region Web Server (TCP/HTTP)

    private static void StartServer()
    {
        try
        {
            int reservedPort;
            TcpListener probe = null;
            try
            {
                probe = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
                probe.Start();
                reservedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally { probe?.Stop(); }

            _boundPort = reservedPort;

            _tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), _boundPort);
            _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _tcpListener.Start();

            File.WriteAllText(PortFilePath, _boundPort.ToString());
            Debug.Log($"[Hermes-Bridge] Wrote port file to: {PortFilePath}");
            File.WriteAllText(TokenFilePath, _authToken);
            Debug.Log($"[Hermes-Bridge] Wrote token file to: {TokenFilePath}");

            _isRunning = true;
            _serverThread = new Thread(ListenLoop) { IsBackground = true };
            _serverThread.Start();

            Debug.Log($"[Hermes-Bridge] Server BOUND to 127.0.0.1 on port: {_boundPort}");
        }
        catch (Exception e) 
        { 
            Debug.LogError($"[Hermes-Bridge] Server failed: {e.Message}\n{e.StackTrace}"); 
        }
    }

    private static void StopServer()
    {
        _isRunning = false;
        try { _tcpListener?.Stop(); } catch { }
        if (_serverThread != null && _serverThread.IsAlive) _serverThread.Join(500);

        // Watcher 해제 추가
        if (_logWatcher != null)
        {
            _logWatcher.EnableRaisingEvents = false;
            _logWatcher.Dispose();
        }

        if (File.Exists(PortFilePath)) File.Delete(PortFilePath);
        if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath);
    }

    private static void ListenLoop()
    {
        while (_isRunning && _tcpListener != null)
        {
            try
            {
                if (!_tcpListener.Pending()) { Thread.Sleep(20); continue; }
                var client = _tcpListener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => ProcessClient(client));
            }
            catch { }
        }
    }

    private static void ProcessClient(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 2000;
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                string requestStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] lines = requestStr.Replace(((char)13).ToString(), "").Split((char)10);
                if (lines.Length == 0 || string.IsNullOrEmpty(lines[0])) return;

                var parts = lines[0].Split(' ');
                if (parts.Length < 2) return;
                string path = parts[1].Split('?')[0];

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(lines[i])) break;
                    var colon = lines[i].IndexOf(':');
                    if (colon > 0) headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
                }

                if (!headers.TryGetValue("X-Hermes-Token", out string clientToken) || clientToken != _authToken)
                {
                    SendResponse(stream, 401, "{\"error\":\"Unauthorized\"}");
                    return;
                }

                if (path == "/health")
                {
                    string healthJson = $"{{\"status\":\"healthy\",\"unityVersion\":\"{_unityVersionCache}\",\"port\":{_boundPort},\"isPlaying\":{_isPlayingCache.ToString().ToLower()}}}";
                    SendResponse(stream, 200, healthJson);
                    return;
                }

                if (path == "/compile-errors")
                {
                    ParseNewLogLines(); // API 요청이 들어왔을 때, 누락된 로그가 없는지 확실하게 한 번 더 파싱

                    string errJson;
                    lock (_lockObject)
                    {
                        errJson = JsonUtility.ToJson(new CompileErrorList { errors = _compiledErrorCache });
                    }
                    SendResponse(stream, 200, errJson);
                    return;
                }

                string responseJson = "{\"status\":\"ok\"}";
                int statusCode = 200;
                var waitHandle = new ManualResetEventSlim(false);

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        switch (path)
                        {
                            case "/compile":
                                lock (_lockObject) _compiledErrorCache.Clear();
                                AssetDatabase.Refresh();
                                responseJson = "{\"message\":\"AssetDatabase refresh triggered.\"}";
                                break;
                            case "/play":
                                EditorApplication.isPlaying = true;
                                responseJson = "{\"message\":\"Entered Play Mode.\"}";
                                break;
                            case "/stop":
                                EditorApplication.isPlaying = false;
                                responseJson = "{\"message\":\"Exited Play Mode.\"}";
                                break;
                            default:
                                statusCode = 404;
                                responseJson = "{\"error\":\"Endpoint not found\"}";
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        statusCode = 500;
                        responseJson = $"{{\"error\":\"{e.Message}\"}}";
                    }
                    finally { waitHandle.Set(); }
                };

                if (!waitHandle.Wait(TimeSpan.FromSeconds(15)))
                {
                    statusCode = 504;
                    responseJson = "{\"error\":\"Gateway timeout\"}";
                }
                SendResponse(stream, statusCode, responseJson);
            }
        }
        catch (Exception) { }
    }

    private static void SendResponse(NetworkStream stream, int statusCode, string json)
    {
        try
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            string statusMessage = statusCode == 200 ? "200 OK" : statusCode == 401 ? "401 Unauthorized" : statusCode == 404 ? "404 Not Found" : "500 Internal Server Error";

            char cr = (char)13;
            char lf = (char)10;
            string eol = $"{cr}{lf}";

            string headerStr = $"HTTP/1.1 {statusMessage}{eol}" +
                               $"Content-Type: application/json; charset=utf-8{eol}" +
                               $"Content-Length: {jsonBytes.Length}{eol}" +
                               $"Connection: close{eol}{eol}";

            byte[] headerBytes = Encoding.UTF8.GetBytes(headerStr);

            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            stream.Flush();
        }
        catch { }
    }

    #endregion
}

[Serializable]
public struct CompileError
{
    public string file;
    public int line;
    public int column;
    public string message;
    public string type;
}

[Serializable]
public class CompileErrorList
{
    public List<CompileError> errors = new List<CompileError>();
}