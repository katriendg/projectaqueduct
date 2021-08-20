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
    public class DeviceInfo
    {
        public DeviceInfo(string twinId, string assetTwinId, double flowCapacity, double flowMargin)
        {
            TwinId = twinId;
            AssetTwinId = assetTwinId;
            FlowCapacity = flowCapacity;
            FlowMargin = flowMargin;
        }

        public string TwinId { get; set; }
        public string AssetTwinId { get; set; }
        public double FlowCapacity { get; set; }
        public double FlowMargin { get; set; }        
    }

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
            var deviceInfos = new List<DeviceInfo>();
            string query = $"SELECT device.$dtId FROM DIGITALTWINS device WHERE IS_OF_MODEL('{modelId}')";
            AsyncPageable<BasicDigitalTwin> queryResult = client.QueryAsync<BasicDigitalTwin>(query);
            await foreach (BasicDigitalTwin twin in queryResult)
            {
                AsyncPageable<BasicRelationship> relationships = client.GetRelationshipsAsync<BasicRelationship>(twin.Id, "isAttachedTo");
                await foreach (BasicRelationship relationship in relationships)
                {
                    Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(relationship.TargetId);
                    BasicDigitalTwin attachedTwin = twinResponse.Value;

                    if (attachedTwin.Contents.TryGetValue("FlowCapacity", out object flowCapacityValue) &&
                        attachedTwin.Contents.TryGetValue("FlowMargin", out object flowMarginValue))
                    {
                        double flowCapacity = ((JsonElement)flowCapacityValue).GetDouble();
                        double flowMargin = ((JsonElement)flowMarginValue).GetDouble();
                        deviceInfos.Add(new DeviceInfo(twin.Id, attachedTwin.Id, flowCapacity, flowMargin));
                    }
                }
            }

            if (deviceInfos.Count > 0)
            {
                Console.WriteLine($"Device twins found:");
                foreach(var deviceInfo in deviceInfos)
                {
                    Console.WriteLine($"  {deviceInfo.TwinId} (attached to {deviceInfo.AssetTwinId}) with flow capacity {deviceInfo.FlowCapacity} and margin {deviceInfo.FlowMargin}.");
                }
                await SimulateSendAdtData(client, deviceInfos);
            }
            else
                Console.WriteLine($"Exiting, no twin IDs found inheriting from {modelId}...");

        }

        private static async Task SimulateSendAdtData(DigitalTwinsClient adtClient, IList<DeviceInfo> deviceInfos)
        {
            var tokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                Console.WriteLine("Exiting...");
            };
            Console.WriteLine("Press CTRL+C to exit");

            await UpdateDeviceProperties(adtClient, deviceInfos, tokenSource.Token);

            tokenSource.Dispose();
        }

        public static async Task UpdateDeviceProperties(DigitalTwinsClient adtClient, IList<DeviceInfo> deviceInfos, CancellationToken cancelToken)
        {
            //Set VolumeFlow, Pressure and Temp based properties
            double avgTemperature = 20.05;
            double avgPressure = 0.5;
            var rand = new Random();

            //first time, props might not have been initiated so do the AppendAdd
            foreach(DeviceInfo deviceInfo in deviceInfos)
            {
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendAdd("/Temperature", avgTemperature);
                updateTwinData.AppendAdd("/Pressure", avgPressure);
                updateTwinData.AppendAdd("/VolumeFlow", deviceInfo.FlowCapacity / 2);
                updateTwinData.AppendAdd("/SensorTimestamp", DateTime.Now);
                await adtClient.UpdateDigitalTwinAsync(deviceInfo.TwinId, updateTwinData);

                Console.WriteLine($"{DateTime.Now} > Added initial Twin properties: {deviceInfo.TwinId}");
            }

            while (!cancelToken.IsCancellationRequested)
            {

                foreach(DeviceInfo deviceInfo in deviceInfos)
                {
                    double currentTemperature = avgTemperature + rand.NextDouble() * 4 - 3;
                    double currentPressure = avgPressure + rand.NextDouble();
                    double currentVolumeFlow = deviceInfo.FlowCapacity * rand.NextDouble();

                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendReplace("/Temperature", currentTemperature);
                    updateTwinData.AppendReplace("/Pressure", currentPressure);
                    updateTwinData.AppendReplace("/VolumeFlow", currentVolumeFlow);
                    updateTwinData.AppendReplace("/SensorTimestamp", DateTime.Now);
                    await adtClient.UpdateDigitalTwinAsync(deviceInfo.TwinId, updateTwinData);

                    Console.WriteLine($"{DateTime.Now} > Updated Twin: {deviceInfo.TwinId} to temp={currentTemperature}, pressure={currentPressure}, volumeflow={currentVolumeFlow}");

                }

                await Task.Delay(5000);
                
            }
        }


    }
}
