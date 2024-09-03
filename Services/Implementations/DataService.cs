using CsvHelper;
using Google.Cloud.Firestore;
using ReStore___backend.Services.Interfaces;
using System.Globalization;

namespace ReStore___backend.Services.Implementations
{
    public class DataService : IDataService
    {
        private readonly FirestoreDb _firestoreDb;

        public DataService()
        {
            // Initialize Firestore client
            _firestoreDb = FirestoreDb.Create("your-project-id");
        }

        public List<dynamic> ProcessData(IEnumerable<dynamic> records)
        {
            // Convert records to a list of dynamic objects
            var recordList = records.ToList();

            // Group by product_id
            var groupedRecords = recordList
                .GroupBy(record => record.product_id)
                .ToList();

            // Save each group to a separate CSV file
            foreach (var group in groupedRecords)
            {
                string productId = group.Key;
                string fileName = $"product_{productId}.csv";

                using (var writer = new StreamWriter(fileName))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(group); // Writes the records of the group to the CSV
                }
            }

            // Return the list of grouped records or some result
            return recordList;
        }
        public async Task SaveDataToFirestore(List<dynamic> processedData)
        {
            var collection = _firestoreDb.Collection("YourCollectionName");

            foreach (var data in processedData)
            {
                string documentId = data.product_id.ToString();

                Dictionary<string, object> firestoreData = new Dictionary<string, object>(data);

                DocumentReference docRef = collection.Document(documentId);
                await docRef.SetAsync(firestoreData);
            }
        }

        public async Task<string> TrainDemandModelEndpoint()
        {
            // Implement the logic to call the TrainDemandModelEndpoint
            return trainResult;
        }

        public async Task<string> PredictDemandEndpoint()
        {
            // Implement the logic to call the PredictDemandEndpoint
            return predictResult;
        }
    }
}
