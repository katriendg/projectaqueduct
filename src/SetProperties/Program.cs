using System;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.Threading.Tasks;
using System.Text.Json;
using Azure;

namespace SetProperties
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string adtInstanceUrl = "https://mvsadt2twin.api.weu.digitaltwins.azure.net";

            var credential = new DefaultAzureCredential();
            var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
            Console.WriteLine($"Service client created – ready to go");

            // Run a query for all twins
            string query = "SELECT device.$dtId, device.$metadata FROM DIGITALTWINS device JOIN target RELATED device.isAttachedTo WHERE target.$dtId = 'Pump1'";
            AsyncPageable<BasicDigitalTwin> queryResult = client.QueryAsync<BasicDigitalTwin>(query);
            await foreach (BasicDigitalTwin twin in queryResult)
            {
                Console.WriteLine(JsonSerializer.Serialize(twin));
            }
        }
    }
}
