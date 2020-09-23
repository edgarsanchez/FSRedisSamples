open Microsoft.Extensions.Configuration
open StackExchange.Redis
open Newtonsoft.Json

type Employee = { Id: string; Name: string; Age: int }

let secretName = "CacheConnection"

let initializeConfiguration () =
    let builder = ConfigurationBuilder().AddUserSecrets<Employee> ()
    builder.Build ()

let lazyConnection (configuration: IConfigurationRoot) secretName =
    lazy (ConnectionMultiplexer.Connect configuration.[secretName])

[< EntryPoint >]
let main _ =
    let configuration = initializeConfiguration ()
    let lazyConnection = lazyConnection configuration secretName

    // Connection refers to a binding that returns a ConnectionMultiplexer
    // as shown in the previous example.
    use connection = lazyConnection.Value
    let cache = connection.GetDatabase ()

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
    printfn "Cache response : %O" (cache.StringSet (RedisKey "Message", RedisValue "Hello! The cache is working from a .NET Core console app!"))

    // Demonstrate "SET Message" executed as expected...
    let cacheCommand4 = "GET Message"
    printfn "\nCache command  : %s or StringGet()" cacheCommand4
    printfn "Cache response : %O" (cache.StringGet (RedisKey "Message"))

    // Get the client list, useful to see if connection list is growing...
    let cacheCommand5 = "CLIENT LIST"
    printfn "\nCache command  : %s" cacheCommand5
    printfn "Cache response : \n%O" (cache.Execute ("CLIENT", "LIST"))

    // Store .NET object to cache
    let e007 = { Id = "007"; Name = "Davide Columbo"; Age = 100 }
    printfn "Cache response from storing Employee .NET object : %O" (cache.StringSet (RedisKey "e007", RedisValue (JsonConvert.SerializeObject e007)))

    // Retrieve .NET object from cache
    let e007FromCache = JsonConvert.DeserializeObject<Employee> (string (cache.StringGet (RedisKey "e007")))
    printfn "Deserialized Employee .NET object :\n"
    printfn "\tEmployee.Name : %s" e007FromCache.Name
    printfn "\tEmployee.Id   : %s" e007FromCache.Id
    printfn "\tEmployee.Age  : %d\n" e007FromCache.Age

    0