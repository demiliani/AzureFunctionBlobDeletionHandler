using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;
using Microsoft.WindowsAzure.Storage;

namespace BlobDeleteProcessing
{
    public static class BlobDeleteFunction
    {
        [FunctionName("HandleBlobDeletion")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            log.LogInformation($"Deleted blob : {data}");
            LogToApplicationInsights($"Deleted blob : {data}");

            string BlobStorageUrl = data?.data.url;
            Uri storage = new Uri(BlobStorageUrl);

            //These parameters can be handled on Azure KeyVault 
            var destinationStorageAccountName = "<destination-storage-name>";
            var sourceStorageAccountName = storage.Host.Split(".").First();
            var sourceKey = "<source-key>";
            var destinationKey = "<destination-key>";

            var containerName = storage.PathAndQuery.Split("/")[1];
            var blobName = storage.PathAndQuery.Split("/").Last();
            CloudBlockBlob sourceBlockBlob = await getCloudBlockBlobReference(sourceKey, sourceStorageAccountName, containerName, blobName, log);
            CloudBlockBlob destinationBlockBlob = await getCloudBlockBlobReference(destinationKey, destinationStorageAccountName, containerName, blobName, log);

            await CopySourceBlobToDestination(sourceBlockBlob, destinationBlockBlob, log);
            return (ActionResult)new OkObjectResult($"{sourceBlockBlob}");
        }

        private static async Task<CloudBlockBlob> getCloudBlockBlobReference(string key, string storageAccountName, string containerName, string blobName, ILogger log)
        {
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={key}";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }

            return container.GetBlockBlobReference(blobName);
        }

        private static async Task CopySourceBlobToDestination(CloudBlockBlob sourceBlob, CloudBlockBlob destinationBlob, ILogger log)
        {
            log.LogInformation("Blob copy started...");
            try
            {
                using (var stream = await sourceBlob.OpenReadAsync())
                {
                    await destinationBlob.UploadFromStreamAsync(stream);
                }
                log.LogInformation("Blob copy completed successfully.");
            }
            catch (Exception ex)
            {
                log.LogError("Blob copy error: " + ex.Message);
            }
            finally
            {
                log.LogInformation("CopySourceBlobToDestination completed");
            }
        }

        private static async void LogToApplicationInsights(string message)
        {
            //Optional custom logging to Azure Application Insights goes here...
        }
    }
}
