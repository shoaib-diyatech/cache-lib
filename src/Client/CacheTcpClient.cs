namespace CacheLibrary;

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CacheCommon;
//using log4net;

/// <summary>
/// Connects to the CacheService and handles requests and responses in different queues and threads. 
/// The CacheClient class will use Request, Response, and Command classes and will map responses to requests using the request ID
/// </summary>
public class CacheTcpClient
{
    //private static readonly ILog log = LogManager.GetLogger(typeof(CacheClient));
    private readonly TcpClient _client;
    private readonly String _ip;

    private readonly int _port;
    private readonly NetworkStream _stream;
    private readonly BlockingCollection<Request> _requestQueue = new();
    private readonly BlockingCollection<Response> _responseQueue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Response>> _pendingRequests = new();
    private readonly string _delimiter = "\r\n";
    private readonly int _bufferSize = 4;

    public CacheTcpClient(string ip, int port)
    {
        _ip = ip;
        _port = port;
        _client = new TcpClient(_ip, _port);
        _stream = _client.GetStream();
    }

    public bool Initialize()
    {
        try
        {
            if (!_client.Connected)
            {
                _client.Connect(_ip, _port);
            }

            Task.Run(() => ProcessRequests());
            Task.Run(() => ProcessResponses());
            return true;
        }
        catch (Exception exp)
        {
            throw new Exception($"Error initializing client: {exp.Message}");
        }
    }

    /// <summary>
    /// Adds the request to the request queue.
    /// Waits for the response with a timeout.
    /// Uses a TaskCompletionSource to map the response to the request using the request ID.
    /// </summary>
    public async Task<Response> SendRequestAsync(Request request, int timeoutMilliseconds = 5000)
    {
        var tcs = new TaskCompletionSource<Response>();
        _pendingRequests[request.RequestId] = tcs;

        _requestQueue.Add(request);

        var cts = new CancellationTokenSource(timeoutMilliseconds);
        cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

        try
        {
            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            _pendingRequests.TryRemove(request.RequestId, out _);
            return new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Type = CacheCommon.ResponseType.Error, Message = "Request timed out." };
        }
    }

    /// <summary>
    ///Sends requests from the request queue to the server
    /// </summary>
    private void ProcessRequests()
    {
        foreach (var request in _requestQueue.GetConsumingEnumerable())
        {
            try
            {
                //string requestJson = JsonSerializer.Serialize(request) + _delimiter;
                //string requestString = request.RequestId +" "+ request.Command.ToString() + _delimiter;
                string requestString = JsonSerializer.Serialize(request) + _delimiter;
                byte[] requestBytes = Encoding.UTF8.GetBytes(requestString);
                _stream.WriteAsync(requestBytes, 0, requestBytes.Length).Wait();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error sending request: {ex.Message}");
                //log.Error($"Error sending request: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reads responses from the server.
    /// Matches responses to requests using the request ID and completes the corresponding TaskCompletionSource
    /// </summary>
    private void ProcessResponses()
    {
        byte[] buffer = new byte[_bufferSize];
        StringBuilder responseBuilder = new StringBuilder();

        while (true)
        {
            try
            {
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                responseBuilder.Append(chunk);

                string accumulatedData = responseBuilder.ToString();
                int delimiterIndex;
                while ((delimiterIndex = accumulatedData.IndexOf(_delimiter)) >= 0)
                {
                    string responseString = accumulatedData.Substring(0, delimiterIndex);
                    accumulatedData = accumulatedData.Substring(delimiterIndex + _delimiter.Length);
                    responseBuilder.Clear();
                    responseBuilder.Append(accumulatedData);

                    Response response = JsonSerializer.Deserialize<Response>(responseString);
                    if (_pendingRequests.TryRemove(response.RequestId, out var tcs))
                    {
                        tcs.SetResult(response);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error receiving response: {ex.Message}");
                //log.Error($"Error receiving response: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Closes the client connection
    /// </summary>
    public void Close()
    {
        _client.Close();
    }
}