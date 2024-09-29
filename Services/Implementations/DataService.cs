using CsvHelper;
using Google.Cloud.Storage.V1;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReStore___backend.Services.Interfaces;
using Firebase.Auth.Provider;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Firebase.Auth.Config;
using IniParser;
using IniParser.Model;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Runtime.CompilerServices;
using ReStore___backend.Dtos;
using Firebase.Auth.Objects;
using Google.Cloud.AIPlatform.V1;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Google;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using CsvHelper.Configuration;

namespace ReStore___backend.Services.Implementations
{
    public class DataService : IDataService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly FirebaseAuthProvider _authProvider;
        private readonly StorageClient _storageClient;
        private readonly HttpClient _httpClient;
        private readonly string _bucketName;
        private readonly string _projectId;
        private readonly string _apiUrl;
        private readonly string _location;
        private readonly string _endpointId;

        public DataService()
        {
            // Configure url and http client
            _httpClient = new HttpClient();
            
            // Load configuration from INI file
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile("credentials.ini");

            // Firebase API key from INI file
            string firebaseApiKey = data["firebase"]["api_key"];
            Console.WriteLine($"API Key: {firebaseApiKey}");

            // Cloud Storage Bucket
            _bucketName = "restore-db-98bee.appspot.com";

            // Load credentials from file explicitly
            GoogleCredential credential;
            using (var stream = new FileStream("/app/restore-db-98bee-8760dc2c521d.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            _storageClient = StorageClient.Create(credential);

            // Firebase Auth
            try
            {
                _authProvider = new FirebaseAuthProvider(new FirebaseConfig(firebaseApiKey));
                Console.WriteLine("Firebase Auth initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Firebase Auth: {ex.Message}");
            }

            // Initialize Firestore using the same credentials
            var firestoreClientBuilder = new FirestoreClientBuilder
            {
                Credential = credential
            };
            var firestoreClient = firestoreClientBuilder.Build();
            _firestoreDb = FirestoreDb.Create("restore-db-98bee", firestoreClient);
        }

        // Sign-up Implementation
        public async Task<string> SignUp(string email, string name, string username, string phoneNumber, string password)
        {
            try
            {
                // Create user using Firebase Authentication
                var auth = await _authProvider.CreateUserWithEmailAndPasswordAsync(email, password);

                // Get the user ID from the created user
                string userId = auth.User.LocalId;

                // Create a document in Firestore to store additional user details
                var userDoc = new Dictionary<string, object>
                {
                    { "email", email },
                    { "name", name },
                    { "username", username },
                    { "phone_number", phoneNumber },
                    { "password", password }
                };

                // Store user details in Firestore under 'Users' collection with the user ID as the document ID
                DocumentReference docRef = _firestoreDb.Collection("Users").Document(userId);
                await docRef.SetAsync(userDoc);

                return "User signed up successfully";
            }
            catch (Exception ex)
            {
                return $"Error during sign-up: {ex.Message}";
            }
        }

        // Login Implementation
        public async Task<LoginResultDTO> Login(string email, string password)
        {
            try
            {
                // Authenticate user with Firebase Auth
                var auth = await _authProvider.SignInWithEmailAndPasswordAsync(email, password);

                // Retrieve the authenticated user's ID
                var userId = auth.User.LocalId;

                // Fetch user data from Firestore or your chosen database
                var userDoc = await _firestoreDb.Collection("Users").Document(userId).GetSnapshotAsync();

                if (!userDoc.Exists)
                {
                    return new LoginResultDTO
                    {
                        Token = auth.FirebaseToken,
                        Username = null
                    };
                }

                // Assuming the username is stored in a field named "username"
                var username = userDoc.GetValue<string>("username") ?? auth.User.Email;

                // Return a DTO with the token and username
                return new LoginResultDTO
                {
                    Token = auth.FirebaseToken,
                    Username = username
                };
            }
            catch (Exception ex)
            {
                // Handle error and return a message
                return new LoginResultDTO
                {
                    Token = $"Error during login: {ex.Message}",
                    Username = null
                };
            }
        }

        // Process data, save to CSV, and upload to Cloud Storage
        public async Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string username)
        {
            var recordList = records.ToList();

            // Group by product_id and process each group
            var groupedRecords = recordList.GroupBy(record => record.ProductID);

            // Create new folder in Cloud Storage
            var folderPath = $"upload_demands/{username}-upload-demands/";

            foreach (var group in groupedRecords)
            {
                string productId = group.Key;
                string fileName = $"product_{productId}.csv";
                var objectName = $"{folderPath}{fileName}";

                // Save the group to a MemoryStream
                using (var memoryStream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        // Write records group to CSV
                        csv.WriteRecords(group);
                    }

                    // Reset the position of the memory stream to the beginning
                    memoryStream.Position = 0;

                    // Upload the CSV file to Cloud Storage
                    await _storageClient.UploadObjectAsync(_bucketName, objectName, null, memoryStream);

                }
            }
        }

        // Fetch the Demand Data from firestore
        public async Task<string> GetDemandDataFromStorageByUsername(string username)
        {
            var demandData = new List<dynamic>();

            // Define the path to the storage bucket and directory
            string folderPath = $"upload_demands/{username}-upload-demands/";

            // List objects in the specified folder
            var storageObjects = _storageClient.ListObjects(_bucketName, folderPath);
            foreach (var storageObject in storageObjects)
            {
                // Only process CSV files
                if (storageObject.Name.EndsWith(".csv"))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        // Download the file to memory
                        await _storageClient.DownloadObjectAsync(_bucketName, storageObject.Name, memoryStream);
                        memoryStream.Position = 0; // Reset the stream position for reading

                        using (var reader = new StreamReader(memoryStream))
                        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                        {
                            var records = csv.GetRecords<dynamic>().ToList();
                            demandData.AddRange(records); // Add records to demandData list
                        }
                    }
                }
            }

            // Group the data by ProductID
            var groupedData = demandData
                .GroupBy(record => record.ProductID) // Change "ProductID" to your desired grouping field
                .Select(group => new
                {
                    ProductID = group.Key,
                    Records = group.ToList()
                });

            // Convert the grouped data to JSON
            string jsonData = JsonConvert.SerializeObject(groupedData, Formatting.Indented);
            return jsonData;
        }

        // Process data, save to CSV, and upload to Cloud Storage for Sales
        public async Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username)
        {
            // Generate a timestamp
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

            // Create a filename
            string fileName = $"sales_{timestamp}.csv";
            string folderPath = $"upload_sales/{username}-upload-sales/";

            // Save the entire records to a MemoryStream
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    // Write all records to the CSV writer
                    csv.WriteRecords(records);
                }

                // Reset the position of the memory stream to the beginning
                memoryStream.Position = 0;

                // Define the object name for cloud storage
                var objectName = $"{folderPath}{fileName}";

                // Upload the CSV file to Cloud Storage
                await _storageClient.UploadObjectAsync(_bucketName, objectName, null, memoryStream);

                // Pass the memory stream to SalesInsight
                memoryStream.Position = 0;
                await SalesInsight(memoryStream, username);
            }
        }

        // Process Sales Data to return as Json
        public async Task<string> GetSalesDataFromStorageByUsername(string username)
        {
            var salesData = new List<SaleRecordDTO>();

            // Define the path to the storage bucket and directory without the trailing slash
            string folderPath = $"upload_sales/{username}-upload-sales";

            // Get the list of objects in the specified folder
            var files = _storageClient.ListObjects(_bucketName, folderPath).ToList();

            // Filter for CSV files
            var csvFiles = files.Where(file => file.Name.EndsWith(".csv")).ToList();

            // Ensure there are CSV files
            if (!csvFiles.Any())
            {
                throw new Exception("No CSV files found in the specified folder.");
            }

            // Get the latest sales file based on the count of CSV files
            var latestFile = csvFiles.Count == 1 ? csvFiles[0] : csvFiles.Last();

            Console.WriteLine($"File name: {latestFile.Name}");

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    // Download the file to memory
                    await _storageClient.DownloadObjectAsync(_bucketName, latestFile.Name, memoryStream);
                    memoryStream.Position = 0;

                    // Convert the file content to string (assuming it's in a supported format, like CSV)
                    using (var reader = new StreamReader(memoryStream))
                    {
                        string content = await reader.ReadToEndAsync();

                        // Split the CSV content into lines
                        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                        // Ensure we have lines to process
                        if (lines.Length > 1)
                        {
                            // Get the header row
                            var header = lines[0].Split(',');

                            // Check if the header at index 1 is not "Sales" and rename it if necessary
                            if (header.Length > 1 && header[1].Trim() != "Sales")
                            {
                                header[1] = "Sales"; // Rename the second column to 'Sales'
                            }
                            else if (header.Length < 2)
                            {
                                throw new Exception("The CSV file does not have enough columns.");
                            }

                            // Skip the header and parse the data starting from the second line
                            for (int i = 1; i < lines.Length; i++)
                            {
                                var columns = lines[i].Split(',');

                                // Check if the row has the correct number of columns
                                if (columns.Length >= 2 && !string.IsNullOrWhiteSpace(columns[0]))
                                {
                                    try
                                    {
                                        // Parse the year and month from the first column
                                        var dateParts = columns[0].Split('-');
                                        if (dateParts.Length != 2) continue;

                                        int year = int.Parse(dateParts[0]);
                                        int month = int.Parse(dateParts[1]);

                                        // Parse the sales amount from the second column (now ensured to be "Sales")
                                        decimal sales = decimal.Parse(columns[1]);

                                        // Create a SaleRecord object and add it to the list
                                        salesData.Add(new SaleRecordDTO
                                        {
                                            Year = year,
                                            Month = month,
                                            Sales = sales
                                        });
                                    }
                                    catch (FormatException ex)
                                    {
                                        Console.WriteLine($"Skipping invalid line: {lines[i]} - Error: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("No valid data rows found in the CSV file.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception details
                Console.WriteLine($"Error processing file: {ex.Message}");
            }

            // Create the desired JSON structure
            var result = salesData
                .GroupBy(sale => sale.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    SalesData = g.Select(s => new
                    {
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(s.Month), // Convert month number to name
                        Sales = s.Sales
                    }).ToList()
                }).ToList();

            // Create a final object to return as JSON
            var finalResult = new { data = result };

            // Serialize to JSON without unwanted characters or newlines
            string jsonData = JsonConvert.SerializeObject(finalResult, Formatting.None);

            // Return the clean JSON string
            return jsonData.Replace("\\n", ""); // Remove any unwanted newlines from the JSON string
        }

        // Call Model for Insight
        public async Task<string> SalesInsight(MemoryStream salesData, string username)
        {
            // Call your API to get the insights
            string insights;
            using (_httpClient)
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StreamContent(salesData), "file", "sales_data.csv");

                string insightUrl = Environment.GetEnvironmentVariable("API_INSIGHT_URL") + "/generate-insights";
                var response = await _httpClient.PostAsync(insightUrl, formData);
                insights = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API call failed: {insights}");
                }
            }

            // Extract the insights text from the JSON response
            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(insights);
            if (!jsonObject.ContainsKey("insights"))
            {
                throw new Exception("Insights not found in the API response.");
            }

            string insightsText = jsonObject["insights"];

            // Format the insights text
            insightsText = insightsText
                .Replace("\n", " ") // Replace new lines with space
                .Replace("\r", " ") // Replace carriage returns with space
                .Replace("\"", "")  // Remove any quotes
                .Replace(",", " ")   // Replace commas with space
                .Replace("  ", " "); // Replace double spaces with a single space

            Console.WriteLine(insightsText); // Log the formatted insights

            // Convert insights to CSV format
            var csvLines = new List<string>
            {
                "InsightData" // Header
            };

            // Add the cleaned insights as the body of the CSV
            csvLines.Add(insightsText);

            string csvContent = string.Join("\n", csvLines);

            Console.WriteLine(csvContent);

            // Upload the CSV to Firebase Storage
            var storagePath = $"Insight/{username}-sales-insight/sales_insights.csv"; // Define your storage path
            using (var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent)))
            {
                await _storageClient.UploadObjectAsync(_bucketName, storagePath, "text/csv", uploadStream);
            }

            return insightsText; // Optionally return the cleaned insights text
        }

        // Get Sales Insight value from Cloud Storage
        public async Task<string> GetSalesInsightByUsername(string username)
        {
            var insightData = new List<InsightDTO>();
            Console.WriteLine($"Username: {username}");

            // Define the path to the storage bucket and directory
            string folderPath = $"Insight/{username}-sales-insight/";
            Console.WriteLine($"Folder path: {folderPath}");

            // Get the list of objects in the specified folder
            var files = _storageClient.ListObjects(_bucketName, folderPath).ToList();

            // Filter for CSV files
            var csvFiles = files.Where(file => file.Name.EndsWith(".csv")).ToList();

            // Ensure there are CSV files
            if (!csvFiles.Any())
            {
                throw new Exception("No CSV files found in the specified folder.");
            }

            // Log the list of files
            Console.WriteLine("Files in the directory: ");
            foreach (var file in files)
            {
                Console.WriteLine($"- {file.Name}");
            }

            // Get the latest sales insight file based on the count of CSV files
            var latestFile = csvFiles[0];

            Console.WriteLine($"File name: {latestFile.Name}");

            using (var memoryStream = new MemoryStream())
            {
                // Download the latest file from cloud storage
                await _storageClient.DownloadObjectAsync(_bucketName, latestFile.Name, memoryStream);
                memoryStream.Position = 0; // Reset stream position

                // Convert CSV data to JSON
                using (var reader = new StreamReader(memoryStream))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<InsightDTO>().ToList();
                    insightData.AddRange(records);
                }
        }

            // Convert the list to JSON and return
            return JsonConvert.SerializeObject(insightData);
        }

        Task<string> IDataService.PredictDemandEndpoint()
        {
            throw new NotImplementedException();
        }

        Task<string> IDataService.TrainDemandModelEndpoint()
        {
            throw new NotImplementedException();
        }

    }
}
