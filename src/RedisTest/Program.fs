open System
open System.Net
open System.Net.Sockets
open Microsoft.Extensions.Configuration
open StackExchange.Redis
open Newtonsoft.Json
open System.Threading

type Employee = { Id: string; Name: string; Age: int }

let secretName = "CacheConnection"

let configuration =
    let builder = ConfigurationBuilder().AddUserSecrets<Employee> ()
    builder.Build ()

let createConnection (configuration: IConfigurationRoot) secretName =
    lazy (ConnectionMultiplexer.Connect configuration.[secretName])

let mutable lazyConnection = createConnection configuration secretName

let connection = lazyConnection.Value

let mutable lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks    
let mutable firstErrorTime = DateTimeOffset.MinValue
let mutable previousErrorTime = DateTimeOffset.MinValue

let reconnectLock = obj()

let reconnectMinFrequency = TimeSpan.FromSeconds 60.
let reconnectErrorThreshold = TimeSpan.FromSeconds 30.

let retryMaxAttempts = 5

let closeConnection (oldConnection: Lazy<ConnectionMultiplexer>) =
    if not (isNull oldConnection) then
        try
            oldConnection.Value.Close()
        with _ ->
            ()

/// Force a new ConnectionMultiplexer to be created.
/// NOTES:
///     1. Users of the ConnectionMultiplexer MUST handle ObjectDisposedExceptions, which can now happen as a result of calling forceReconnect().
///     2. Don't call forceReconnect for Timeouts, just for RedisConnectionExceptions or SocketExceptions.
///     3. Call this method every time you see a connection exception. The code will:
///         a. wait to reconnect for at least the "ReconnectErrorThreshold" time of repeated errors before actually reconnecting
///         b. not reconnect more frequently than configured in "ReconnectMinFrequency"
let forceReconnect () =
    let previousTicks = Interlocked.Read &lastReconnectTicks
    let previousReconnectTime = DateTimeOffset(previousTicks, TimeSpan.Zero)
    let elapsedSinceLastReconnectStart = DateTimeOffset.UtcNow - previousReconnectTime

    if elapsedSinceLastReconnectStart >= reconnectMinFrequency then
        lock reconnectLock ( fun () -> 
            let utcNow = DateTimeOffset.UtcNow
            let elapsedSinceLastReconnect = utcNow - previousReconnectTime

            if firstErrorTime = DateTimeOffset.MinValue then
                // We haven't seen an error since last reconnect, so set initial values.
                firstErrorTime <- utcNow
                previousErrorTime <- utcNow
            elif elapsedSinceLastReconnect >= reconnectMinFrequency then
                let elapsedSinceFirstError = utcNow - firstErrorTime
                let elapsedSinceMostRecentError = utcNow - previousErrorTime
                
                let shouldReconnect = elapsedSinceFirstError >= reconnectErrorThreshold
                                        && elapsedSinceMostRecentError <= reconnectErrorThreshold

                // Update the previousErrorTime timestamp to be now (e.g. this reconnect request).
                previousErrorTime <- utcNow

                if shouldReconnect then
                    firstErrorTime <- DateTimeOffset.MinValue
                    previousErrorTime <- DateTimeOffset.MinValue

                    let oldConnection = lazyConnection
                    closeConnection oldConnection
                    lazyConnection <- createConnection configuration secretName
                    Interlocked.Exchange (&lastReconnectTicks, utcNow.UtcTicks) |> ignore
        )

// In real applications, consider using a framework such as
        // Polly to make it easier to customize the retry approach.
let basicRetry<'T> (func: unit -> 'T) =
    let rec retry reconnectRetry disposedRetry =
        try
            func ()
        with
        | :? RedisConnectionException
        | :? SocketException ->
            if reconnectRetry >= retryMaxAttempts then
                reraise ()
            else
                forceReconnect ()
                retry (reconnectRetry + 1) disposedRetry
        | :? ObjectDisposedException ->
            if disposedRetry >= retryMaxAttempts then
                reraise ()
            else
                retry reconnectRetry  (disposedRetry + 1)
        
    retry 0 0

// let getDatabase () = basicRetry (fun _ -> connection.GetDatabase())
let getDatabase () = basicRetry connection.GetDatabase

// let getEndPoints () = basicRetry (fun _ -> connection.GetEndPoints())
let getEndPoints () = basicRetry connection.GetEndPoints

let getServer (host: string) (port: int) =
    basicRetry (fun () -> connection.GetServer(host, port))

[< EntryPoint >]
let main _ =
    let cache = getDatabase ()

    // Perform cache operations using the cache object...

    // Simple PING command
    let cacheCommand = "PING"
    printfn "\nCache command : %s" cacheCommand
    printfn "Cache response : %O" (cache.Execute cacheCommand) 

    // Simple get and put of integral data types into the cache
    let cacheCommand2 = "GET Message";
    printfn "\nCache command  : %s or StringGet()" cacheCommand2
    printfn "Cache response : %O" (cache.StringGet (RedisKey "Message"))

    let cacheCommand3 = "SET Message \"Hello! The cache is working from a .NET Core console app!\""
    printfn "\nCache command  : %s or StringSet()" cacheCommand3
    printfn "Cache response : %O" (cache.StringSet (RedisKey"Message", RedisValue "Hello! The cache is working from a .NET Core console app!"))

    // Demonstrate "SET Message" executed as expected...
    let cacheCommand4 = "GET Message"
    printfn "\nCache command  : %s or StringGet()" cacheCommand4
    printfn "Cache response : %O" (cache.StringGet (RedisKey "Message"))

    // Get the client list, useful to see if connection list is growing...
    let cacheCommand5 = "CLIENT LIST"
    printfn "\nCache command  : %s" cacheCommand5
    let endpoint = getEndPoints().[0] :?> DnsEndPoint
    let server = getServer endpoint.Host endpoint.Port
    let clients = server.ClientList()
    printfn "Cache response:"
    for client in clients do
        printfn "%s" client.Raw

    // printfn "Cache response : \n%O" (cache.Execute ("CLIENT", "LIST"))

    // Store .NET object to cache
    let e007 = { Id = "007"; Name = "Davide Columbo"; Age = 100 }
    printfn "Cache response from storing Employee .NET object : %O" (cache.StringSet (RedisKey "e007", RedisValue (JsonConvert.SerializeObject e007)))

    // Retrieve .NET object from cache
    let e007FromCache = JsonConvert.DeserializeObject<Employee> (string (cache.StringGet (RedisKey "e007")))
    printfn "Deserialized Employee .NET object :\n"
    printfn "\tEmployee.Name : %s" e007FromCache.Name
    printfn "\tEmployee.Id   : %s" e007FromCache.Id
    printfn "\tEmployee.Age  : %d\n" e007FromCache.Age

    closeConnection lazyConnection
    0