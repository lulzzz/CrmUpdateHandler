using CrmUpdateHandler.Utility;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

[assembly: FunctionsStartup(typeof(CrmUpdateHandler.Startup))]

namespace CrmUpdateHandler
{

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Dependency Injection became available in May 2019: <see cref="https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection"/></remarks>
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            //builder.Services.AddHttpClient();
            //builder.Services.AddSingleton((s) => {
            //    return new CosmosClient(Environment.GetEnvironmentVariable("COSMOSDB_CONNECTIONSTRING"));
            //});

            // This worked, but it wasn't enough. Waiting till new fixes or better documentation comes out before leaping into Dependency Injection
            //builder.Services.AddSingleton<IHubSpotAdapter, HubspotAdapter>();
        }
    }
}
