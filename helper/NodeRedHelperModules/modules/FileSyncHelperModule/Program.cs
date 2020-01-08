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
    using System.Net.Http;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class Program
    {
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

        static string restApiServerModuleName;
        static string restApiServerModulePort;
        static HttpClient httpClient = null;
        static void InitRestAPICalling()
        {
            restApiServerModuleName = System.Environment.GetEnvironmentVariable("RESTAPI_SERVER_MODULE_NAME");
            restApiServerModulePort = System.Environment.GetEnvironmentVariable("RESTAPI_SERVER_MODULE_PORT");
            if (string.IsNullOrEmpty(restApiServerModuleName)||string.IsNullOrEmpty(restApiServerModulePort)){
                Console.WriteLine("REST API SeRVER settings are not enough");
            }else{
                httpClient = new HttpClient();
            }
        }


        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            await InitLocalBlobStorageAccess();
            InitRestAPICalling();

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetMethodHandlerAsync(nameof(UploadFileToCloud), UploadFileToCloud, ioTHubModuleClient);
            await ioTHubModuleClient.SetMethodHandlerAsync(nameof(ReplaceFileFromCloud), ReplaceFileFromCloud, ioTHubModuleClient);
            await ioTHubModuleClient.SetMethodHandlerAsync(nameof(InvokeRESTService), InvokeRESTService, ioTHubModuleClient);
        }

        private static async Task<MethodResponse> InvokeRESTService(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("{0} is invoked",nameof(InvokeRESTService));
            MethodResponse responseMessage = null;
            if (httpClient== null){
                string unsetMessage = "REST API Server module is not defined";
                Console.WriteLine(unsetMessage);
                responseMessage = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(unsetMessage),500);
            }
            else{
                string payload = methodRequest.DataAsJson;
                Console.WriteLine("payload -'{0}'", payload);
                dynamic payloadJson = Newtonsoft.Json.JsonConvert.DeserializeObject(payload);
                dynamic restApiMethodIn = payloadJson.method;
                string restApiMethod = restApiMethodIn.Value;
                dynamic restApiUriIn = payloadJson.uri;
                string restApiUri = restApiUriIn.Value;
                dynamic restApiBodyIn = payloadJson.body;
                string restApiBody = restApiBodyIn.Value;
                dynamic restHeaderIn = payloadJson.header;
                string restHeader = restHeaderIn.Value;
                var httpContent = new StringContent(restApiBody);
                var headerValues = restHeader.Split(new char [] {','});
                HttpResponseMessage httpResponse = null;
                string invokeUri = "http://"+restApiServerModuleName + ":" + restApiServerModulePort + restApiUriIn;
                Console.WriteLine("Invoking - " + invokeUri);
                HttpRequestMessage requestMessage = null;
                switch (restApiMethod){
                    case "GET":
                        requestMessage = new HttpRequestMessage(HttpMethod.Get, invokeUri);
                        break;
                    case "POST":
                        requestMessage = new HttpRequestMessage(HttpMethod.Post, invokeUri);
                        break;
                    default:
                        Console.WriteLine("REST API method should be 'GET' or 'POST'");
                        break;
                }
                if (requestMessage!=null){
                    requestMessage.Content = httpContent;
                    foreach(var hr in headerValues) {
                        if (!string.IsNullOrEmpty(hr)){
                            var hrs = hr.Split(new char [] {':'});
                            requestMessage.Headers.Add(hrs[0],hrs[1]);
                        }
                    }
                    try{
                        Console.WriteLine("Send start");
                        httpResponse = await httpClient.SendAsync(requestMessage);
                        Console.WriteLine("Send end");
                    }
                    catch (Exception ex) {
                        Console.WriteLine("SendAsync exception - " + ex.Message);
                        string unsetMessage = "Exception happened - '" + ex.Message + "'";
                        responseMessage = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(unsetMessage),500);
                    }
                }
                if (httpResponse!=null){
                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK){
                        Console.WriteLine("REST API "+restApiMethod+" - Succeeded");
                    }
                    else {
                        Console.WriteLine("REST API "+restApiMethod+" - Wrong - "+ httpResponse.StatusCode);
                    }
                    responseMessage = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(await httpResponse.Content.ReadAsStringAsync()),(int) httpResponse.StatusCode);
                }
            }
            return responseMessage;
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
