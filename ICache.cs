namespace CacheLibrary;
using System;



    public interface ICache : IDisposable
    {
        /// <summary>
        /// Establishes a connection with the caching server.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Adds a new object to the cache with the given key, keeps the ttl as 0, which means the item will never expire, theoretically.
        /// Throws an exception if the key already exists.
        /// </summary>
        void Add(string key, object value);

         void Add(string key, object value, int ttl);

        /// <summary>
        /// Retrieves the cached object as a raw value.
        /// Returns null if the key does not exist.
        /// </summary>
        object Get(string key);

        /// <summary>
        /// Retrieves the cached object and deserializes it into type T.
        /// Returns default(T) if the key does not exist.
        /// </summary>
        T Get<T>(string key);

        /// <summary>
        /// Updates the object in the cache for the given key, updates the ttl to 0, which means the item will never expire, theoretically.
        /// Throws an exception if the key does not exist.
        /// </summary>
        void Update(string key, object value);

        /// <summary>
        /// Updates the object in the cache for the given key.
        /// Throws an exception if the key does not exist.
        /// </summary>
        void Update(string key, object value, int ttl);

        /// <summary>
        /// Removes the object from the cache for the given key.
        /// Does nothing if the key does not exist.
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        void Clear();
    }