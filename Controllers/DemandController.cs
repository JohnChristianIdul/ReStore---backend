using Microsoft.AspNetCore.Http;
using CsvHelper;
using ExcelDataReader;
using Firebase.Auth;
using Google.Cloud.Firestore;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;
using static Google.Cloud.Firestore.V1.StructuredQuery.Types;
using System.Globalization;

namespace ReStore___backend.Controllers
{

    [ApiController]
    [Route("api/[controller]")]

    public class DemandController : Controller
    {

        private readonly FirestoreDb _firestoreDb;
        string projectID = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID_RESTORE");

        public DemandController()
        {
            // Initialize Firestore
            _firestoreDb = FirestoreDb.Create(projectID);
        }

        // GET: DemandController
        [HttpGet("upload/demands")]
        public ActionResult UploadDemands()
        {
            return Ok();
        }

        // POST: DemandController/Create
        [HttpPost("upload/demand")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDemandFile(IFormFile file)
        {

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            // Generate a unique filename
            var filename = Path.GetRandomFileName + "_" + DateTime.Now.ToString("yyyymmddhhmm") + "_" + file.FileName;
            var extension = Path.GetExtension(filename);

            string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads\\Documents", filename);

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            try
            {
                // Check if the file is a CSV or Excel file
                if (file.FileName.EndsWith(".csv"))
                {
                    // Save the CSV file to the server
                    using (var stream = new FileStream(uploadPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Read CSV file
                    using (var reader = new StreamReader(uploadPath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>().ToList();
                        var processResult = ProcessData(records); // Custom processing method

                        // Save processed data to Firestore
                        await SaveDataToFirestore(processResult);

                        // Simulate calling external endpoints
                        var trainResult = await TrainDemandModelEndpoint();
                        var predictResult = await PredictDemandEndpoint();

                        return Ok(new
                        {
                            success = "CSV file uploaded successfully",
                            filename = filename,
                            result = processResult,
                            trainResult = trainResult,
                            predictResult = predictResult
                        });
                    }
                }
                else if (file.FileName.EndsWith(".xlsx") || file.FileName.EndsWith(".xls"))
                {
                    // Save the Excel file to the server
                    string excelPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "loaded_data_demand.xlsx");
                    using (var stream = new FileStream(excelPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Read Excel file
                    using (var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read))
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet();
                        // Convert Excel data to CSV (optional) or process directly
                        var csvData = ConvertExcelToCsv(result); // Convert method
                        var processResult = ProcessData(csvData);

                        // Save processed data to Firestore
                        await SaveDataToFirestore(processResult);

                        // Simulate calling external endpoints
                        var trainResult = await TrainDemandModelEndpoint();
                        var predictResult = await PredictDemandEndpoint();

                        return Ok(new
                        {
                            success = "Excel file uploaded successfully",
                            filename = filename,
                            trainResult = trainResult,
                            predictResult = predictResult
                        });
                    }
                }
                else
                {
                    return BadRequest(new { error = "Invalid file format. Please upload a CSV or Excel file." });
                }
            }
            catch (System.Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
