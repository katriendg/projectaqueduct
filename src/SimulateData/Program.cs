using System;
using System.Net.Http;
using Azure.DigitalTwins.Core;
using Azure;
using Azure.Identity;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace SimulateData
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            
             IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.local.json", optional: false, reloadOnChange: false)
                    .Build();
            
            string adtInstanceUrl = config["ADT:ADT_URI"];
            string modelId = "dtmi:sample:aqueduct:device:Base;1";

            var credentials = new DefaultAzureCredential();
            var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), credentials);
            Console.WriteLine($"ADT service client connection created.");

            //Get all twins based on the device:Base interface
            string query = $"SELECT device.$dtId, device.$metadata FROM DIGITALTWINS device WHERE IS_OF_MODEL('{modelId}')";
            AsyncPageable<BasicDigitalTwin> queryResult = client.QueryAsync<BasicDigitalTwin>(query);
            var reslist = new List<BasicDigitalTwin>();
            await foreach (BasicDigitalTwin twin in queryResult)
            {
                reslist.Add(twin);
            }

            Console.WriteLine($"Device twins found: {reslist.Count}.");

            if (reslist.Count > 0)
                await SimulateSendAdtData(client, reslist.ToArray());
            else
                Console.WriteLine($"Exiting, no twin IDs found inheriting from {modelId}...");

        }

        private static async Task SimulateSendAdtData(DigitalTwinsClient adtClient, BasicDigitalTwin[] deviceTwinIds)
        {
            var tokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                Console.WriteLine("Exiting...");
            };
            Console.WriteLine("Press CTRL+C to exit");

            await UpdateDeviceProperties(adtClient, deviceTwinIds, tokenSource.Token);

            tokenSource.Dispose();
        }

        public static async Task UpdateDeviceProperties(DigitalTwinsClient adtClient, BasicDigitalTwin[] deviceTwinIds, CancellationToken cancelToken)
        {
            //Set VolumeFlow, Pressure and Temp based properties
            double avgTemperature = 20.05;
            double avgVolumeFlow = 5;
            double avgPressure = 0.5;
            var rand = new Random();

            //first time, props might not have been initiated so do the AppendAdd
            for (int i = 0; i < deviceTwinIds.Length; i++)
            {
                
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendAdd("/Temperature", avgTemperature);
                updateTwinData.AppendAdd("/Pressure", avgPressure);
                updateTwinData.AppendAdd("/VolumeFlow", avgVolumeFlow);
                await adtClient.UpdateDigitalTwinAsync(deviceTwinIds[i].Id, updateTwinData);

                Console.WriteLine($"{DateTime.Now} > Added initial Twin properties: {deviceTwinIds}");
            }

            while (!cancelToken.IsCancellationRequested)
            {

                for (int i = 0; i < deviceTwinIds.Length; i++)
                {
                    double currentTemperature = avgTemperature + rand.NextDouble() * 4 - 3;
                    double currentPressure = avgPressure + rand.NextDouble();
                    double currentVolumeFlow = avgVolumeFlow + rand.NextDouble();


                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendReplace("/Temperature", currentTemperature);
                    updateTwinData.AppendReplace("/Pressure", currentPressure);
                    updateTwinData.AppendReplace("/VolumeFlow", currentVolumeFlow);
                    await adtClient.UpdateDigitalTwinAsync(deviceTwinIds[i].Id, updateTwinData);

                    Console.WriteLine($"{DateTime.Now} > Updated Twin: {deviceTwinIds}");

                }

                await Task.Delay(5000);
                
            }
        }


    }
}
