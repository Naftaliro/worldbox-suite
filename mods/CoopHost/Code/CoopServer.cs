using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace CoopHost
{
    [Serializable]
    public class CoopActionRequest
    {
        public string guestName;
        public string type;   // "cursor", "spawn", "terrain", "power"
        public string asset;  // species/tile/power id
        public int x;
        public int y;
        public int radius;
    }

    /// <summary>
    /// Lightweight zero-admin TcpListener HTTP/1.1 server so LAN / Tailscale / playit.gg
    /// connections work on Windows without requiring UAC elevation or netsh urlacl rules.
    /// </summary>
    public class CoopHttpServer
    {
        private readonly int _port;
        private readonly string _webClientPath;
        private readonly Func<string> _getPin;
        private readonly Func<string> _getStateJson;
        private readonly Func<string> _getTerrainJson;
        private readonly Action<CoopActionRequest> _enqueueAction;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private long _lastClientTicks = 0;

        public bool IsRunning => _running;
        public bool HasRecentClient => _lastClientTicks > 0 && (DateTime.UtcNow.Ticks - _lastClientTicks) < TimeSpan.TicksPerSecond * 12;
        public string LastError { get; private set; } = "";

        public CoopHttpServer(
            int port,
            string webClientPath,
            Func<string> getPin,
            Func<string> getStateJson,
            Func<string> getTerrainJson,
            Action<CoopActionRequest> enqueueAction)
        {
            _port = port;
            _webClientPath = webClientPath;
            _getPin = getPin;
            _getStateJson = getStateJson;
            _getTerrainJson = getTerrainJson;
            _enqueueAction = enqueueAction;
        }

        public bool Start()
        {
            if (_running) return true;
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _running = true;
                LastError = "";

                _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "CoopHostHttp" };
                _acceptThread.Start();
                Debug.Log($"[CoopHost] Listening on port {_port} (LAN / Tailscale / playit.gg ready)");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                _running = false;
                Debug.LogError($"[CoopHost] Failed to start server on port {_port}: {e.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 4000;
                    client.SendTimeout = 4000;
                    using NetworkStream ns = client.GetStream();

                    string rawReq = ReadHttpRequest(ns, out string body);
                    if (string.IsNullOrEmpty(rawReq)) return;

                    string firstLine = rawReq.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
                    string[] parts = firstLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpperInvariant();
                    string fullTarget = parts[1];
                    string path = fullTarget;
                    string query = "";
                    int qIdx = fullTarget.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        path = fullTarget.Substring(0, qIdx);
                        query = fullTarget.Substring(qIdx + 1);
                    }

                    if (method == "OPTIONS")
                    {
                        WriteResponse(ns, 204, "text/plain", Array.Empty<byte>());
                        return;
                    }

                    if (method == "GET" && (path == "/" || path == "/index.html"))
                    {
                        _lastClientTicks = DateTime.UtcNow.Ticks;
                        byte[] html = File.Exists(_webClientPath)
                            ? File.ReadAllBytes(_webClientPath)
                            : Encoding.UTF8.GetBytes("<h1>CoopHost WebClient/index.html missing</h1>");
                        WriteResponse(ns, 200, "text/html; charset=utf-8", html);
                        return;
                    }

                    // All /api/* endpoints check the 4-digit join PIN
                    string expectedPin = _getPin?.Invoke() ?? "";
                    string suppliedPin = ExtractQueryParam(query, "pin");
                    if (!string.IsNullOrEmpty(expectedPin) && !string.Equals(expectedPin, suppliedPin, StringComparison.Ordinal))
                    {
                        WriteJson(ns, 401, "{\"error\":\"Invalid join PIN\"}");
                        return;
                    }

                    _lastClientTicks = DateTime.UtcNow.Ticks;

                    if (method == "GET" && path == "/api/state")
                    {
                        WriteJson(ns, 200, _getStateJson?.Invoke() ?? "{}");
                        return;
                    }

                    if (method == "GET" && path == "/api/terrain")
                    {
                        WriteJson(ns, 200, _getTerrainJson?.Invoke() ?? "{}");
                        return;
                    }

                    if (method == "POST" && path == "/api/action")
                    {
                        var act = JsonConvert.DeserializeObject<CoopActionRequest>(body);
                        if (act != null)
                        {
                            _enqueueAction?.Invoke(act);
                            WriteJson(ns, 200, "{\"ok\":true}");
                        }
                        else
                        {
                            WriteJson(ns, 400, "{\"error\":\"Invalid JSON action\"}");
                        }
                        return;
                    }

                    WriteJson(ns, 404, "{\"error\":\"Not found\"}");
                }
                catch
                {
                    // Ignore client disconnect errors
                }
            }
        }

        private static string ReadHttpRequest(NetworkStream ns, out string body)
        {
            body = "";
            var ms = new MemoryStream();
            byte[] buf = new byte[4096];
            int headerEnd = -1;
            int contentLength = 0;

            while (true)
            {
                int read = ns.Read(buf, 0, buf.Length);
                if (read <= 0) break;
                ms.Write(buf, 0, read);

                byte[] arr = ms.ToArray();
                if (headerEnd < 0)
                {
                    headerEnd = FindHeaderEnd(arr);
                    if (headerEnd >= 0)
                    {
                        string hdr = Encoding.ASCII.GetString(arr, 0, headerEnd);
                        foreach (string line in hdr.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(line.Substring(15).Trim(), out contentLength);
                            }
                        }
                    }
                }

                if (headerEnd >= 0 && arr.Length >= headerEnd + 4 + contentLength)
                {
                    string headerStr = Encoding.ASCII.GetString(arr, 0, headerEnd);
                    if (contentLength > 0)
                    {
                        body = Encoding.UTF8.GetString(arr, headerEnd + 4, Math.Min(contentLength, arr.Length - (headerEnd + 4)));
                    }
                    return headerStr;
                }
                if (ms.Length > 262144) break;
            }
            return "";
        }

        private static int FindHeaderEnd(byte[] arr)
        {
            for (int i = 0; i <= arr.Length - 4; i++)
            {
                if (arr[i] == 13 && arr[i + 1] == 10 && arr[i + 2] == 13 && arr[i + 3] == 10)
                    return i;
            }
            return -1;
        }

        private static string ExtractQueryParam(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return "";
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && string.Equals(pair.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
            }
            return "";
        }

        private static void WriteJson(NetworkStream ns, int status, string json)
        {
            WriteResponse(ns, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }

        private static void WriteResponse(NetworkStream ns, int status, string contentType, byte[] payload)
        {
            string reason = status == 200 ? "OK" : (status == 204 ? "No Content" : (status == 401 ? "Unauthorized" : "Error"));
            string header =
                $"HTTP/1.1 {status} {reason}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n";
            byte[] hBytes = Encoding.ASCII.GetBytes(header);
            ns.Write(hBytes, 0, hBytes.Length);
            if (payload.Length > 0)
            {
                ns.Write(payload, 0, payload.Length);
            }
        }

        public static string GetLocalLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint ep) return ep.Address.ToString();
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
