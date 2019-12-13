namespace SampleModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class Program
    {
        static int counter;
        static string deviceId = null; 

        static void Main(string[] args)
        {
            deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
            Console.WriteLine("Device Id - '"+deviceId+"'");

            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }
        static Azure.Storage.Blobs.BlobServiceClient blobServiceClient = null;
        static Azure.Storage.Blobs.BlobContainerClient blobContainerClient = null;

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task InitLocalBlobStorageAccess()
        {
            string blobOnEdgeAccountName = System.Environment.GetEnvironmentVariable("BLOB_ON_EDGE_ACCOUNT_NAME");
            string blobOnEdgeAccountKey = System.Environment.GetEnvironmentVariable("BLOB_ON_EDGE_ACCOUNT_KEY");
            string blobOnEdgeModuleName = System.Environment.GetEnvironmentVariable("BLOB_ON_EDGE_MODULE");
            string blobContainerName = System.Environment.GetEnvironmentVariable("BLOB_ON_EDGE_CONTAINER_NAME");
            Console.WriteLine("Blob on Edge Module Name - "+blobOnEdgeModuleName);
            Console.WriteLine("{Name,Key} = "+blobOnEdgeAccountName+","+blobOnEdgeAccountKey);
            Console.WriteLine("Blob Folder - "+blobContainerName);

            string localBlobConnectionString = "DefaultEndpointsProtocol=http;BlobEndpoint=http://"+blobOnEdgeModuleName+":11002/"+blobOnEdgeAccountName+";AccountName="+blobOnEdgeAccountName+";AccountKey="+blobOnEdgeAccountKey+";";
            Console.WriteLine("Connection string - "+localBlobConnectionString);
            try{
                blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(localBlobConnectionString);
                blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                await blobContainerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
                Console.WriteLine("Floder created");
            }
            catch(Exception ex){
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            await InitLocalBlobStorageAccess();

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetMethodHandlerAsync(nameof(UploadFileToCloud), UploadFileToCloud, ioTHubModuleClient);
            await ioTHubModuleClient.SetMethodHandlerAsync(nameof(ReplaceFileFromCloud), ReplaceFileFromCloud, ioTHubModuleClient);
        }

        private static async Task<MethodResponse> UploadFileToCloud(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Invoked - " + nameof(UploadFileToCloud));
            MethodResponse response = null;
            string payload = methodRequest.DataAsJson;
            Console.WriteLine("Received - '"+payload+"'");

            try{
                dynamic payloadJson = Newtonsoft.Json.JsonConvert.DeserializeObject(payload);
                Console.WriteLine("Desirialized");
                dynamic filenameIn = payloadJson.filename;
                Console.WriteLine("filenameIn - '"+filenameIn);
                var now = DateTime.Now;
                string filename = filenameIn.Value;
                Console.WriteLine("Got filename - '"+filename);
                var fileInfo = new FileInfo(filename);
                var uploadFileName = deviceId + "-" + now.ToString("yyyyMMddHHmmss")+"-"+fileInfo.Name;
                Console.WriteLine("File name will be - "+uploadFileName);

                using(var fs = File.Open(filename,FileMode.Open, FileAccess.Read)){
                    Console.WriteLine("FIle existed");
                    await blobContainerClient.UploadBlobAsync(uploadFileName,fs);
                }
                var message= new {
                    uploadedfilename = uploadFileName
                };
                response=new MethodResponse(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message)),200);
                Console.WriteLine("Uploaded - " + uploadFileName);
            }
            catch(Exception ex){
                Console.WriteLine("Exception - "+ex.Message);
                var message = "payload should include 'filename'";
                response = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(message),500);
                Console.WriteLine("payload missing - '"+payload);
            }
            return response;
        }

        private static async Task<MethodResponse> ReplaceFileFromCloud(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Invoked - " + nameof(UploadFileToCloud));
            MethodResponse response = null;
            string payload = methodRequest.DataAsJson;
            try {
                dynamic payloadJson = Newtonsoft.Json.JsonConvert.DeserializeObject(payload);
                dynamic filenameIn = payloadJson.filename;
                dynamic fileContentIn = payloadJson.content;

                string filename = filenameIn.Value;
                string content = fileContentIn.Value;

                using(var fs = File.Open(filename,FileMode.OpenOrCreate,FileAccess.Write)){
                    using(var writer = new StreamWriter(fs)){
                        await writer.WriteAsync(content);
                        await writer.FlushAsync();
                    }
                }
                var message= new {
                    status = "replaced.",
                    filename=filename
                };
                response=new MethodResponse(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message)),200);
            }
            catch (Exception ex) {
                Console.WriteLine("Exception - "+ex.Message);
                var message = "payload should include 'filename' and 'contents'";
                response = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(message),500);
                Console.WriteLine("payload missing - '"+payload);
            }
            return response;
        }

    }
}
