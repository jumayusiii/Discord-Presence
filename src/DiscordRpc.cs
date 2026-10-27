using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DiscordPresence
{
    /// <summary>
    /// Minimal Discord Rich Presence client. Talks to the running Discord
    /// desktop client over its local IPC named pipe, so no third-party
    /// library or network connection is required.
    /// </summary>
    public sealed class DiscordRpc : IDisposable
    {
        private const int OpHandshake = 0;
        private const int OpFrame = 1;
        private const int OpClose = 2;
        private const int OpPing = 3;
        private const int OpPong = 4;
        private const int WriteTimeoutMs = 4000;
        private const int ResponseTimeoutMs = 6000;

        private readonly object _sync = new object();
        private readonly object _writeLock = new object();
        private readonly object _pendingLock = new object();
        private readonly Dictionary<string, PendingRequest> _pending = new Dictionary<string, PendingRequest>();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly int _pid = Process.GetCurrentProcess().Id;

        private NamedPipeClientStream _pipe;
        private volatile int _generation;
        private volatile bool _connected;
        private bool _disposed;

        public bool IsConnected
        {
            get { return _connected; }
        }

        public event Action<bool> ConnectionChanged;
        public event Action<string> Notice;

        private sealed class PendingRequest
        {
            public readonly ManualResetEventSlim Signal = new ManualResetEventSlim(false);
            public Dictionary<string, object> Response;
        }

        /// <summary>Connects and performs the RPC handshake. Returns null on success.</summary>
        public string Connect(string clientId)
        {
            if (_disposed)
            {
                return "The app is shutting down.";
            }
            if (string.IsNullOrEmpty(clientId))
            {
                return "Enter your Discord Application ID first.";
            }
            clientId = clientId.Trim();
            ulong parsed;
            if (!ulong.TryParse(clientId, out parsed))
            {
                return "The Application ID must contain digits only.";
            }

            lock (_sync)
            {
                _generation++;
                ClosePipeLocked();

                NamedPipeClientStream pipe = null;
                for (int index = 0; index < 10; index++)
                {
                    NamedPipeClientStream candidate = null;
                    try
                    {
                        // Overlapped I/O lets a pending read coexist with a write.
                        // Without it, .NET serializes the two and clear/update calls block.
                        candidate = new NamedPipeClientStream(".", "discord-ipc-" + index,
                            PipeDirection.InOut, PipeOptions.Asynchronous);
                        candidate.Connect(120);
                        pipe = candidate;
                        break;
                    }
                    catch
                    {
                        if (candidate != null)
                        {
                            try { candidate.Dispose(); } catch { }
                        }
                    }
                }

                if (pipe == null)
                {
                    return "Discord isn't running. Open the Discord desktop app and try again.";
                }

                try
                {
                    Dictionary<string, object> handshake = new Dictionary<string, object>();
                    handshake["v"] = 1;
                    handshake["client_id"] = clientId;
                    WriteFrame(pipe, OpHandshake, _json.Serialize(handshake));
                }
                catch
                {
                    try { pipe.Dispose(); } catch { }
                    return "Could not talk to Discord. Try restarting the Discord app.";
                }

                int generation = _generation;
                _pipe = pipe;
                _connected = true;
                ReadLoopAsync(pipe, generation);
            }

            RaiseConnection(true);
            return null;
        }

        /// <summary>Publishes an activity, or clears it when activity is null.</summary>
        public string SetActivity(Dictionary<string, object> activity)
        {
            NamedPipeClientStream pipe;
            lock (_sync)
            {
                pipe = _pipe;
            }
            if (!_connected || pipe == null)
            {
                return "Discord is not connected.";
            }

            string nonce = Guid.NewGuid().ToString("N");
            PendingRequest pending = new PendingRequest();
            lock (_pendingLock)
            {
                _pending[nonce] = pending;
            }

            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>();
                args["pid"] = _pid;
                args["activity"] = activity;

                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["cmd"] = "SET_ACTIVITY";
                payload["nonce"] = nonce;
                payload["args"] = args;

                WriteFrame(pipe, OpFrame, _json.Serialize(payload));

                if (!pending.Signal.Wait(ResponseTimeoutMs))
                {
                    return "Discord did not respond. Try again in a moment.";
                }

                Dictionary<string, object> response = pending.Response;
                if (response == null)
                {
                    return "Lost the connection to Discord. Try again.";
                }
                string evt = response.ContainsKey("evt") ? response["evt"] as string : null;
                if (string.Equals(evt, "ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    return FriendlyError(ExtractError(response));
                }
                return null;
            }
            catch (Exception)
            {
                Disconnect();
                return "Lost the connection to Discord. Try again.";
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pending.Remove(nonce);
                }
                try { pending.Signal.Dispose(); } catch { }
            }
        }

        public string ClearActivity()
        {
            return SetActivity(null);
        }

        public void Disconnect()
        {
            bool notify;
            lock (_sync)
            {
                _generation++;
                notify = _connected;
                ClosePipeLocked();
            }
            if (notify)
            {
                RaiseConnection(false);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            lock (_sync)
            {
                _generation++;
                ClosePipeLocked();
            }
        }

        private async void ReadLoopAsync(NamedPipeClientStream pipe, int generation)
        {
            try
            {
                byte[] header = new byte[8];
                while (generation == _generation)
                {
                    if (!await ReadExactlyAsync(pipe, header, header.Length).ConfigureAwait(false))
                    {
                        break;
                    }
                    int opcode = BitConverter.ToInt32(header, 0);
                    int length = BitConverter.ToInt32(header, 4);
                    if (length < 0 || length > 4 * 1024 * 1024)
                    {
                        break;
                    }

                    byte[] payload = new byte[length];
                    if (length > 0 && !await ReadExactlyAsync(pipe, payload, length).ConfigureAwait(false))
                    {
                        break;
                    }

                    if (opcode == OpPing)
                    {
                        try
                        {
                            WriteFrame(pipe, OpPong, Encoding.UTF8.GetString(payload));
                        }
                        catch
                        {
                            break;
                        }
                        continue;
                    }
                    if (opcode == OpClose)
                    {
                        break;
                    }
                    if (opcode == OpFrame && length > 0)
                    {
                        HandleFrame(Encoding.UTF8.GetString(payload));
                    }
                }
            }
            catch
            {
                // Broken pipe, app shutdown, or Discord closing: fall through.
            }
            finally
            {
                HandleUnexpectedDisconnect(generation);
            }
        }

        private void HandleUnexpectedDisconnect(int generation)
        {
            if (generation != _generation)
            {
                return;
            }

            bool notify = false;
            lock (_sync)
            {
                if (generation == _generation)
                {
                    _generation++;
                    _connected = false;
                    NamedPipeClientStream old = _pipe;
                    _pipe = null;
                    if (old != null)
                    {
                        try { old.Dispose(); } catch { }
                    }
                    FailPending();
                    notify = true;
                }
            }
            if (notify)
            {
                RaiseConnection(false);
            }
        }

        private void HandleFrame(string json)
        {
            Dictionary<string, object> message;
            try
            {
                message = _json.DeserializeObject(json) as Dictionary<string, object>;
            }
            catch
            {
                return;
            }
            if (message == null)
            {
                return;
            }

            string nonce = message.ContainsKey("nonce") ? message["nonce"] as string : null;
            if (!string.IsNullOrEmpty(nonce))
            {
                PendingRequest pending = null;
                lock (_pendingLock)
                {
                    if (_pending.TryGetValue(nonce, out pending))
                    {
                        _pending.Remove(nonce);
                    }
                }
                if (pending != null)
                {
                    pending.Response = message;
                    try { pending.Signal.Set(); } catch { }
                    return;
                }
            }

            string evt = message.ContainsKey("evt") ? message["evt"] as string : null;
            if (string.Equals(evt, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                RaiseNotice(FriendlyError(ExtractError(message)));
            }
        }

        private void WriteFrame(NamedPipeClientStream pipe, int opcode, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json ?? "{}");
            byte[] frame = new byte[8 + payload.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(opcode), 0, frame, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, frame, 4, 4);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);
            }

            lock (_writeLock)
            {
                Task write = pipe.WriteAsync(frame, 0, frame.Length);
                if (!write.Wait(WriteTimeoutMs))
                {
                    throw new IOException("Timed out writing to Discord.");
                }
            }
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
                if (read <= 0)
                {
                    return false;
                }
                offset += read;
            }
            return true;
        }

        private void ClosePipeLocked()
        {
            NamedPipeClientStream oldPipe = _pipe;
            _pipe = null;
            _connected = false;

            if (oldPipe != null)
            {
                try { oldPipe.Dispose(); } catch { }
            }
            FailPending();
        }

        private void FailPending()
        {
            List<PendingRequest> pending;
            lock (_pendingLock)
            {
                pending = new List<PendingRequest>(_pending.Values);
                _pending.Clear();
            }
            foreach (PendingRequest request in pending)
            {
                try { request.Signal.Set(); } catch { }
            }
        }

        private static string ExtractError(Dictionary<string, object> message)
        {
            if (message.ContainsKey("data"))
            {
                Dictionary<string, object> data = message["data"] as Dictionary<string, object>;
                if (data != null)
                {
                    if (data.ContainsKey("message") && data["message"] != null)
                    {
                        return Convert.ToString(data["message"]);
                    }
                    if (data.ContainsKey("code") && data["code"] != null)
                    {
                        return "Discord error " + Convert.ToString(data["code"]) + ".";
                    }
                }
            }
            return "Discord rejected the request.";
        }

        private static string FriendlyError(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "Discord rejected the request.";
            }
            if (raw.IndexOf("invalid client id", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Discord rejected this Application ID. Check it in the Developer Portal.";
            }
            if (raw.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Discord is rate-limiting updates. Wait a few seconds and try again.";
            }
            if (raw.IndexOf("not running", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Discord isn't running. Open the Discord desktop app and try again.";
            }
            return raw;
        }

        private void RaiseConnection(bool connected)
        {
            Action<bool> handler = ConnectionChanged;
            if (handler == null)
            {
                return;
            }
            try { handler(connected); } catch { }
        }

        private void RaiseNotice(string message)
        {
            Action<string> handler = Notice;
            if (handler == null)
            {
                return;
            }
            try { handler(message); } catch { }
        }
    }
}
