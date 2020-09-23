# F# Redis Samples
This repo contains several Redis samples implemented in F#. So far I've got:
* The [.NET Core Quickstart](https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-dotnet-core-quickstart) in the src/RedisTest folder
  * The sample connects to an Azure Cache for Redis service, the instructions on how to set it up are [here](https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-dotnet-core-quickstart#create-a-cache)
  * The credentials for connecting to the Redis service are stored as secrets in the console app assembly, the instructions on how to do this are the same as for the C# example, the gist of it is the command `dotnet user-secrets set CacheConnection "<cache name>.redis.cache.windows.net,abortConnect=false,ssl=true,password=<primary-access-key>"`, detailed instructions [here](https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-dotnet-core-quickstart#add-secret-manager-to-the-project)
  * To run the sample, after doing the setup steps, get into the src/RedisTest folder and enter `dotnet run`

Comments and feedback are welcomed!
