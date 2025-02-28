namespace CacheLibrary;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CacheCommon;

public class CacheImplementation : ICache
{
    private readonly CacheTcpClient _client;

    public CacheImplementation(string host, int port, string cacheName)
    {
        _client = new CacheTcpClient(host, port);
    }

    public void Initialize()
    {
        _client.Initialize();
    }

    public void Add(string key, object value)
    {
        Add(key, value, 0);
    }

    public void AddAsync(string key, object value, int ttl)
    {
        // Fire-and-forget: This starts an async task without waiting for it to complete.
        // The underscore `_` is used to discard the returned Task to avoid compiler warnings.
        _ = Task.Run(async () =>
        {
            try
            {
                // Create a CancellationTokenSource that will automatically cancel after 5 seconds.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                // Serialize the object using MessagePack to reduce data size before transmission.
                //byte[] serializedValue = MessagePackSerializer.Serialize(value);
                string serializedValue = JsonSerializer.Serialize(value);

                // Create the request object that will be sent to the cache server.

                //var request = new Request
                //{
                //    RequestId = Guid.NewGuid().ToString(), // Generate a unique request ID.
                //    Command = new CreateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl)
                //};

                var request = Request.Create(new CreateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl));

                // Send the request to the cache server asynchronously.
                // sendTask represents the actual async operation that sends the request.
                var sendTask = _client.SendRequestAsync(request);

                // Step 4: Wait for either the `sendTask` to complete OR for the timeout (5 seconds) to expire.
                var completedTask = await Task.WhenAny(sendTask, Task.Delay(Timeout.Infinite, cts.Token));

                // Explanation of Task.WhenAny():
                // - `sendTask` is the actual operation that communicates with the cache server.
                // - `Task.Delay(Timeout.Infinite, cts.Token)` will be canceled automatically after 5 seconds.
                // - `Task.WhenAny()` returns the first task that completes.

                if (completedTask == sendTask) // If the request finished before the timeout
                {
                    // Ensure any exceptions from sendTask are thrown.
                    Response response = await sendTask;

                    // Validate the response message to check if the add operation was successful.
                    if (!response.Code.Equals(Code.Success))
                    {
                        throw new Exception($"Add operation failed: {response.Message}");
                    }
                }
                else
                {
                    // The timeout occurred before the sendTask could complete.
                    throw new TimeoutException("Add operation timed out");
                }
            }
            catch (Exception ex)
            {
                // Since this is fire-and-forget, we cannot propagate exceptions.
                // Instead, we log the error. You can replace this with log4net or any logging mechanism.
                Console.WriteLine($"Error adding to cache: {ex.Message}");
            }
        });
    }

    public void Add(string key, object value, int ttl)
    {
        // Fire-and-forget: This starts an async task without waiting for it to complete.
        // The underscore `_` is used to discard the returned Task to avoid compiler warnings.

        try
        {
            // Create a CancellationTokenSource that will automatically cancel after 5 seconds.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Serialize the object using MessagePack to reduce data size before transmission.
            //byte[] serializedValue = MessagePackSerializer.Serialize(value);
            // Not using MessagePack for now
            string serializedValue = JsonSerializer.Serialize(value);

            // Create the request object that will be sent to the cache server.
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(), // Generate a unique request ID.
            //    Command = new CreateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl)
            //};
            ICommand cmd = new CreateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl);
            Request request = Request.Create(cmd);

            // Send the request to the cache server asynchronously. But wait for it to complete.
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();
            // Validate the response message to check if the add operation was successful.
            if (!response.Code.Equals(Code.Success))
            {
                throw new Exception($"Add operation failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            // Since this is fire-and-forget, we cannot propagate exceptions.
            // Instead, we log the error. You can replace this with log4net or any logging mechanism.
            Console.WriteLine($"Error adding to cache: {ex.Message}");
        }

    }

    public object Get(string key)
    {
        try
        {
            // Create the request
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(),
            //    Command = new ReadCommand(key)
            //};
            var request = Request.Create(new ReadCommand(key));

            // Send the request and wait for the response
            //Response response = await _client.SendRequestAsync(request);
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(response.Message) || response.Message.Trim() == "NULL")
            {
                return null;
            }
            else
            {
                //return response.Value;
                if (response.Value != null)
                {
                    // Convert Base64 string to byte array
                    byte[] jsonBytes = Convert.FromBase64String(response.Value.ToString());

                    // Convert byte array to JSON string
                    string jsonString = Encoding.UTF8.GetString(jsonBytes);

                    // Deserialize JSON string to an object
                    return JsonSerializer.Deserialize<object>(jsonString);
                }
                else
                    return null;
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error getting from cache", ex);
        }
    }

    public T Get<T>(string key)
    {

        //// Step 1: Convert Base64 string to byte array
        //byte[] jsonBytes = Convert.FromBase64String(base64String);

        //// Step 2: Convert byte array to JSON string
        //string jsonString = Encoding.UTF8.GetString(jsonBytes);

        //// Step 3: Deserialize JSON string to an object
        //return JsonSerializer.Deserialize<T>(jsonString);



        object value = Get(key);
        if (value == null)
        {
            return default;
        }

        return (T)value;
    }

    public void Update(string key, object value, int ttl)
    {
        try
        {
            // Serialize the object
            string serializedValue = JsonSerializer.Serialize(value);

            // Create the request
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(),
            //    Command = new UpdateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl)
            //};
            var request = Request.Create(new UpdateCommand(key, Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedValue)), ttl));

            // Send the request and wait for the response
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();

            if (!response.Code.Equals(Code.Success))
            {
                throw new Exception($"Update operation failed: {response.Message}");
            }
            return;
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
            // Create the request
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(),
            //    Command = new DeleteCommand(key)
            //};
            var request = Request.Create(new DeleteCommand(key));

            // Send the request and wait for the response
            //Response response = await _client.SendRequestAsync(request);
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();


            if (string.IsNullOrWhiteSpace(response.Message) || response.Message.Trim() == "NULL")
            {
                return;
            }
            else
            {
                return;
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error getting from cache", ex);
        }
    }

    public void Clear()
    {
        try
        {
            // Create the request
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(),
            //    Command = new FlushAllCommand()
            //};
            var request = Request.Create(new FlushAllCommand());

            // Send the request and wait for the response
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();

            if (!response.Code.Equals(Code.Success))
            {
                throw new Exception($"Clear operation failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Error clearing cache", ex);
        }
    }

    public string Memory()
    {
        try
        {
            // Create the request
            //var request = new Request
            //{
            //    RequestId = Guid.NewGuid().ToString(),
            //    Command = new MemCommand()
            //};
            var request = Request.Create(new MemCommand());

            // Send the request and wait for the response
            Response response = _client.SendRequestAsync(request).GetAwaiter().GetResult();

            return response.Message;
        }
        catch (Exception ex)
        {
            throw new Exception("Error getting memory from cache", ex);
        }
    }

    public void Dispose()
    {
        //_client.Dispose();
    }
}