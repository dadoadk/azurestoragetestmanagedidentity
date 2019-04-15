using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.Net.Http;
using System.Threading;

namespace AzureStorageTestApp
{
    class Program
    {
        static string azureResourceID = ConfigurationManager.AppSettings["azureResourceID"];
        static string azureStorageBlobHostUrl = ConfigurationManager.AppSettings["azureStorageBlobHostUrl"];
        static string blobContainerName = ConfigurationManager.AppSettings["azureStorageContainerName"];

        static async Task Main(string[] args)
        {
            string command;
            bool quitNow = false;

            Console.WriteLine("Welcome to Azure Blob Storage testing app.");
            Console.WriteLine("");
            Console.WriteLine("Menu options:");
            Console.WriteLine("1. - Start with the test.");
            Console.WriteLine("2. - Review the app settings values.");
            Console.WriteLine("quit - to close the app.");
            Console.WriteLine("");
            while (!quitNow)
            {
                command = Console.ReadLine();
                switch (command)
                {
                    case "1":
                        Console.WriteLine("Type in name or path of the file you would like to upload to test the connection to Azure Blob Storage:");
                        Console.WriteLine("( example: c://file1.jpg )");
                        string fileName = Console.ReadLine();
                        
                        try
                        {
                            FileStream fs = File.OpenRead(fileName);

                            Console.WriteLine("The file is found and read. [" + fileName + ",  " + fs.Length + " byte size]");
                            Console.WriteLine("");
                            byte[] bytes = new byte[fs.Length];
                            fs.Read(bytes, 0, Convert.ToInt32(fs.Length));
                            fs.Close();
                            await TestStorageAsync(bytes);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " Use menu options and try again. (1, 2, Quit)");
                        }                        
                        

                        break;

                    case "2":
                        Console.WriteLine("azureResourceID: " + azureResourceID);
                        Console.WriteLine("");
                        Console.WriteLine("azureStorageBlobHostUrl: " + azureStorageBlobHostUrl);
                        Console.WriteLine("");
                        Console.WriteLine("blobContainerName: " + blobContainerName);
                        Console.WriteLine("");
                        Console.WriteLine("Use menu options (1, 2, Quit)");
                        break;

                    case "quit":
                        quitNow = true;
                        break;

                    case "q":
                        quitNow = true;
                        break;

                    case "Quit":
                        quitNow = true;
                        break;

                    case "Q":
                        quitNow = true;
                        break;

                    default:
                        Console.WriteLine("Unknown Command " + command);
                        break;
                }
            }
        }

        public static async Task TestStorageAsync(byte[] file)
        {
            List<string> MSItokenResult = await GetMSITokenWithClientAsync();

            try
            {
                if (MSItokenResult.Count() > 0)
                {
                    CloudStorageAccount.UseV1MD5 = false;
                    //get token
                    string accessToken = MSItokenResult.FirstOrDefault();

                    //create token credential
                    TokenCredential tokenCredential = new TokenCredential(accessToken);

                    //create storage credentials
                    StorageCredentials storageCredentials = new StorageCredentials(tokenCredential);

                    string fileName = Guid.NewGuid().ToString() + ".jpeg";

                    // Create a block blob using the credentials.
                    CloudBlockBlob blob = new CloudBlockBlob(new Uri(azureStorageBlobHostUrl + "/" + blobContainerName + "/" + fileName), storageCredentials);

                    await blob.UploadFromByteArrayAsync(file, 0, file.Count());

                    Console.WriteLine("Blob url: " + blob.Uri.ToString());
                    Console.WriteLine("");
                    Console.WriteLine("File uploaded successfully using storage auth with managed identity.");
                    Console.WriteLine("");
                    Console.WriteLine("Use menu options (1, 2, Quit)");
                }
                else
                {
                    Console.WriteLine("Skipped creating storage credentials for blob storage, an error occured while acquiring MSI token");
                    Console.WriteLine("");
                    Console.WriteLine("Use menu options and try again. (1, 2, Quit)");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);
                Console.WriteLine("");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("");
                Console.WriteLine(ex.HelpLink);
                Console.WriteLine("");
                Console.WriteLine("Use menu options and try again. (1, 2, Quit)");
            }


        }
        

        static async Task<List<string>> GetMSITokenWithClientAsync()
        {
            List<string> responseResult = new List<string>();

            HttpResponseMessage returnVal = new HttpResponseMessage();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // *** Create a request messaage ***
                    HttpRequestMessage getRequest = new HttpRequestMessage();

                    // *** Get URL ***
                    getRequest.RequestUri = new Uri("http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=" + azureResourceID);

                    // *** Set method ***
                    getRequest.Method = HttpMethod.Get;

                    // *** Add the headers ***
                    getRequest.Headers.Add("Metadata", "true");

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    // *** Send the message ***
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    returnVal = await client.SendAsync(getRequest);

                    stopwatch.Stop();

                    string jsonstringResponse = await returnVal.Content.ReadAsStringAsync();

                    Dictionary<string, string> list = (Dictionary<string, string>)JsonConvert.DeserializeObject(jsonstringResponse, typeof(Dictionary<string, string>));

                    string accessToken = string.Empty;

                    accessToken = list["access_token"];

                    responseResult.Add(accessToken);

                    // Stop timing.
                    stopwatch.Stop();
                    Console.WriteLine("MSI token acquired successfully " + stopwatch.ElapsedMilliseconds + "ms");

                    return responseResult;
                }
            }
            catch (Exception ex)
            {
                string errorText = String.Format("{0} \n\n{1}", ex.Message, ex.InnerException != null ? ex.InnerException.Message : "Failed to acquire token");
                Console.WriteLine(errorText, ex);
                Console.WriteLine("");

                return responseResult;
            }           
        }
    }
}
