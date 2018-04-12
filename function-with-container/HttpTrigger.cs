using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Management.ContainerInstance;
using Microsoft.Azure.Management.ContainerInstance.Models;
using Microsoft.Rest;
using Microsoft.Azure.WebJobs;

namespace ImageProcessorCore
{
    public static class ImageProcessor
    {
        /// <summary>
        /// The resource group name.
        /// </summary>
        public static string ResourceGroupName = Environment.GetEnvironmentVariable("RESOURCE_GROUP");

        /// <summary>
        /// The ACI container group name.
        /// </summary>
        public const string ContainerGroupName = "function-extension";

        /// <summary>
        /// Gets or sets the container endpoint.
        /// </summary>
        public static string ApiEndpoint { get; set; }

        /// <summary>
        /// The http client.
        /// </summary>
        public static HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// The container lifetime, by when it will be killed.
        /// </summary>
        public static DateTime ContainerLifetime = DateTime.UtcNow.AddMinutes(5);

        /// <summary>
        /// The healthcheck function that simply returns 200.
        /// </summary>
        public static HttpResponseMessage HealthCheck(HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            return req.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// The process function that creates container if not exists, and proxies requests.
        /// </summary>
        public async static Task<IActionResult> Process(HttpRequest req, TraceWriter log)
        {
            ImageProcessor.ContainerLifetime = DateTime.UtcNow.AddMinutes(20);
            log.Info($"Process http request, extending container life time to {ImageProcessor.ContainerLifetime}.");

            if (string.IsNullOrEmpty(ImageProcessor.ApiEndpoint))
            {
                await ImageProcessor.InitializeContainerEndpoint(log);

                if (string.IsNullOrEmpty(ImageProcessor.ApiEndpoint))
                {
                    return new ContentResult { Content = "Container not available yet.", StatusCode = 429 };
                }
            }

            log.Info("Proxy request to: " + ImageProcessor.ApiEndpoint);

            var resp = await ImageProcessor.HttpClient.GetAsync(ImageProcessor.ApiEndpoint);
            var content = await resp.Content.ReadAsStringAsync();

            return new ContentResult { Content = content, StatusCode = (int?)resp.StatusCode };
        }

        /// <summary>
        /// The cleanup function that deletes container if no request in past 20 minutes.
        /// </summary>
        public static async Task Cleanup(TimerInfo myTimer, TraceWriter log)
        {
            if (ImageProcessor.ContainerLifetime > DateTime.UtcNow)
            {
                log.Info($"Container lifetime '{ImageProcessor.ContainerLifetime}' is later than '{DateTime.UtcNow}'. No cleanup needed.");

                return;
            }

            log.Info($"Container lifetime '{ImageProcessor.ContainerLifetime}' is earlier than '{DateTime.UtcNow}'. Do cleanup.");
            ImageProcessor.ApiEndpoint = null;
            await ImageProcessor.DeleteContainer(log);
        }

        public async static Task DeleteContainer(TraceWriter log)
        {
            var token = await AuthUtil.GetToken();
            var client = new ContainerInstanceManagementClient(new TokenCredentials(token));
            client.SubscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");

            if (string.IsNullOrEmpty(client.SubscriptionId))
            {
                throw new InvalidOperationException("Environment variale 'SUBSCRIPTION_ID' not set.");
            }

            log.Info($"Deleting container '{ImageProcessor.ContainerGroupName}'");
            await client.ContainerGroups.DeleteAsync(ResourceGroupName, ImageProcessor.ContainerGroupName);
        }

        public async static Task InitializeContainerEndpoint(TraceWriter log)
        {
            var token = await AuthUtil.GetToken();
            var client = new ContainerInstanceManagementClient(new TokenCredentials(token));
            client.SubscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");

            if (string.IsNullOrEmpty(client.SubscriptionId))
            {
                throw new InvalidOperationException("Environment variale 'SUBSCRIPTION_ID' not set.");
            }

            var containers = await client.ContainerGroups.ListByResourceGroupAsync(ResourceGroupName);
            var container = containers.Where(c => c.Name == ImageProcessor.ContainerGroupName).FirstOrDefault();

            if (container == null || container.IpAddress == null || container.IpAddress.Ip == null)
            {
                log.Warning("Container not found, going to create...");

                var spec = new ContainerGroup
                {
                    Location = "East US",
                    OsType = "Linux",
                    RestartPolicy = "Always",
                    IpAddress = new IpAddress
                    {
                        Ports = new[] { new Port(9000) }
                    },
                    Containers = new[]{
                        new Container
                        {
                            Name = "imaginary",
                            Image = "h2non/imaginary",
                            Ports = new []{ new ContainerPort(9000) },
                            Resources = new ResourceRequirements
                            {
                                Requests = new ResourceRequests(memoryInGB: 1.5, cpu: 1)
                            }
                        }
                    }
                };

                await client.ContainerGroups.CreateOrUpdateAsync(
                    resourceGroupName: ImageProcessor.ResourceGroupName,
                    containerGroupName: ImageProcessor.ContainerGroupName,
                    containerGroup: spec);
            }
            else
            {
                log.Info("Container IP: " + container.IpAddress.Ip);
                log.Info("Container port: " + container.IpAddress.Ports.FirstOrDefault().PortProperty);

                ImageProcessor.ApiEndpoint = "http://" + container.IpAddress.Ip + ":" + container.IpAddress.Ports.FirstOrDefault().PortProperty;
            }
        }
    }
}
