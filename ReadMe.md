# CacheLibrary

## Overview
**CacheLibrary** is a .NET class library that provides an easy-to-use client for interacting with a remote TCP-based caching server. It enables applications to store, retrieve, update, and delete key-value pairs over a network.

## .NET Compatibility
This library is built for **.NET 9.0** and requires a .NET environment that supports this version.

## Features
- **Connects to a remote caching server** over TCP.
- Supports **CRUD operations** (`CREATE`, `READ`, `UPDATE`, `DELETE`) on cached data.
- Allows setting a **TTL (Time-to-Live)** for cache entries.
- Uses **JSON serialization** for storing complex objects.
- **Asynchronous communication** for better performance.

## Installation & Integration (Manual Setup)
To use **CacheLibrary** in a client project, follow these steps:

1. **Copy the `cache-lib.dll` file** into the `libs/` folder inside your client project.
2. **Modify the client project's `.csproj` file** to include a reference to the library:
   ```xml
   <Reference Include="CacheLibrary">
     <HintPath>libs/cache-lib.dll</HintPath>
   </Reference>
