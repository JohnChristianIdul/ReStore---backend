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

namespace ReStore___backend.Services.Implementations
{
    public class DataService : IDataService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly FirebaseAuthProvider _authProvider;
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public DataService()
        {
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
            using (var stream = new FileStream("/app/restore-db-98bee-fac0b3ad66ae.json", FileMode.Open, FileAccess.Read))
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
        public async Task<string> SignUp(string email, string name, string password, string phoneNumber, string username)
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
                    { "phone_number", phoneNumber },
                    { "username", username },
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
        public async Task<string> Login(string email, string password)
        {
            try
            {
                // Authenticate user with Firebase Auth
                var auth = await _authProvider.SignInWithEmailAndPasswordAsync(email, password);

                return $"User logged in successfully. Token: {auth.FirebaseToken}";
            }
            catch (Exception ex)
            {
                return $"Error during login: {ex.Message}";
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

            // Ensure folder exists by uploading an empty file to it
            await CreateFolderInCloudStorage(folderPath);

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

        // Process data, save to CSV, and upload to Cloud Storage for Sales
        public async Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username)
        {
            // Generate a timestamp
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

            // Create a filename
            string fileName = $"sales_{timestamp}.csv";
            string folderPath = $"upload_sales/{username}-upload-sales/";

            // Ensure folder exists by uploading an empty file to it
            await CreateFolderInCloudStorage(folderPath);

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
            }
        }

        ////Extract year from csv file for folder name
        //private string ExtractYear(string dateString)
        //{
        //    DateTime date;
        //    string format_1 = "yyyy-MM";
        //    string format_2 = "yyyy-MM-dd";
        //    string[] formats = { format_1, format_2 };

        //    if (DateTime.TryParseExact(dateString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        //    {
        //        return dateString;
        //    }
        //    else
        //    {
        //        throw new FormatException($"Date String '{dateString}' is not in a recognized format. Format must be yyyy-MM or yyyy-MM-DD.");
        //    }
        //}

        // Method to create a folder in Cloud Storage
        private async Task CreateFolderInCloudStorage(string folderPath)
        {
            // Creating a folder in Cloud Storage is done by uploading an empty object with a trailing slash
            var emptyObjectName = $"{folderPath}/";
            using (var memoryStream = new MemoryStream())
            {
                await _storageClient.UploadObjectAsync(_bucketName, emptyObjectName, null, memoryStream);
            }
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
