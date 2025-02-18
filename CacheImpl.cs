namespace CacheLibrary;
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MessagePack;

public class CacheClient : ICache
{
    private readonly string _serverIp;
    private readonly int _port;
    private readonly string _cacheName;
    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public CacheClient(string serverIp, int port, string cacheName)
    {
        _serverIp = serverIp;
        _port = port;
        _cacheName = cacheName;
    }

    public void Initialize()
    {
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.ConnectAsync(_serverIp, _port).GetAwaiter().GetResult();
            _networkStream = _tcpClient.GetStream();
            _isInitialized = true;
            //Console.WriteLine($"Connected to caching server at {_serverIp}:{_port} using cache '{_cacheName}'.");
            //return _isInitialized;
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error connecting to caching server at {_serverIp}:{_port} using cache '{_cacheName}'.");
            //Console.WriteLine(ex.Message);
            throw new Exception("Error connecting to caching server.", ex);
        }
    }

    /// <summary>
    /// Sends a command to the server and returns the response.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The response from the server.</returns>
    /// <exception cref="IOException">Thrown when there is an error sending or receiving data.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
    private string SendCommand(string command)
    {
        EnsureInitialized();
        try
        {
            byte[] commandBytes = Encoding.UTF8.GetBytes(command);
            _networkStream.WriteAsync(commandBytes, 0, commandBytes.Length)
                          .GetAwaiter().GetResult();
            byte[] buffer = new byte[1024];
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10))) // Set timeout to 10 seconds
            {
                int bytesRead = _networkStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)
                                              .GetAwaiter().GetResult();
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return response;
            }
        }
        catch (Exception ex)
        {
            throw new IOException("An error occurred while sending or receiving data.", ex);
        }
    }

    public void Add(string key, object value, int ttl)
    {
        try
        {
            EnsureInitialized();
            string jsonValue = JsonSerializer.Serialize(value);
            //Encoding.UTF8.GetBytes(jsonValue);
            string base64Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonValue));

            string command;
            if (ttl <= 0)
            {
                command = $"CREATE {key} {base64Value}";
            }
            else
            {
                command = $"CREATE {key} {base64Value} {ttl}";
            }
            string response = SendCommand(command);
            if (!response.StartsWith("OK"))
            {
                //throw new Exception($"Add operation failed: {response}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error adding to cache", ex);
        }
    }




    /// <summary>
    /// Adds the value as MessagePack's encoded string 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="ttl"></param>
    /// <exception cref="Exception"></exception>
    public void AddMP(string key, object value, int ttl)
    {
        try
        {
            EnsureInitialized();

            // Serialize the object using MessagePack
            byte[] serializedValue = MessagePackSerializer.Serialize(value);

            // Build the command
            string command;
            if (ttl <= 0)
            {
                command = $"CREATE {key} {Convert.ToBase64String(serializedValue)}"; // Sending the base64 string over TCP
            }
            else
            {
                command = $"CREATE {key} {Convert.ToBase64String(serializedValue)} {ttl}";
            }

            string response = SendCommand(command);
            if (!response.StartsWith("OK"))
            {
                // Handle the failure response
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error adding to cache", ex);
        }
    }


    public void Add(string key, object value)
    {
        Add(key, value, 0);
    }


    public object Get(string key)
    {
        try
        {
            EnsureInitialized();
            string command = $"READ {key}";
            string response = SendCommand(command);
            if (string.IsNullOrWhiteSpace(response) || response.Trim() == "NULL")
            {
                return null;
            }

            // Decode Base64 and return the JSON string
            return Encoding.UTF8.GetString(Convert.FromBase64String(response));
        }
        catch (Exception ex)
        {
            throw new Exception("Error getting from cache", ex);
        }
    }

    /// <summary>
    /// Generic version of Get. Deserializes the JSON response into the specified type.
    /// </summary>
    public T Get<T>(string key)
    {
        string jsonValue = Get(key) as string; // Retrieve and cast to string
        if (string.IsNullOrWhiteSpace(jsonValue))
        {
            return default;
        }

        // Deserialize JSON into the requested type
        return JsonSerializer.Deserialize<T>(jsonValue);
    }

    public void Update(string key, object value, int ttl)
    {
        try
        {
            EnsureInitialized();
            string jsonValue = JsonSerializer.Serialize(value);

            string command;
            if (ttl <= 0)
            {
                command = $"UPDATE {key} {jsonValue}";
            }
            else
            {
                command = $"UPDATE {key} {jsonValue} {ttl}";
            }
            string response = SendCommand(command);
            if (!response.StartsWith("OK"))
            {
                //throw new Exception($"Update operation failed: {response}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error updating cache", ex);
        }
    }

    public void Update(string key, object value)
    {
        Update(key, value, 0);
    }

    public void Remove(string key)
    {
        try
        {
            EnsureInitialized();
            string command = $"DELETE {key}";
            SendCommand(command);
        }
        catch (Exception ex)
        {
            throw new Exception("Error removing from cache", ex);
        }
    }

    public void Clear()
    {
        try
        {
            EnsureInitialized();
            string command = "CLEAR";
            string response = SendCommand(command);
            if (!response.StartsWith("OK"))
            {
                throw new Exception($"Clear operation failed: {response}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error clearing cache", ex);
        }
    }

    public void Dispose()
    {
        if (_networkStream != null)
        {
            _networkStream.Close();
            _networkStream = null;
        }
        if (_tcpClient != null)
        {
            _tcpClient.Close();
            _tcpClient = null;
        }
        _isInitialized = false;
        Console.WriteLine("Cache client disposed and connection closed.");
    }

    public string Memory()
    {
        try
        {
            EnsureInitialized();
            string command = $"MEM ?";
            string response = SendCommand(command);
            if (string.IsNullOrWhiteSpace(response) || response.Trim() == "NULL")
            {
                return response;
            }
            return response;
        }
        catch (Exception ex)
        {
            throw new Exception("Error getting memory from cache", ex);
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Cache client is not initialized. Call Initialize() first.");
        }
    }


}
