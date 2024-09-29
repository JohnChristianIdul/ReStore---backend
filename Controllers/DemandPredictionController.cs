using CsvHelper;
using ExcelDataReader;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemandPredictionController : ControllerBase
    {
        private readonly IDataService _dataService;

        // Constructor injection of IDataService
        public DemandPredictionController(IDataService dataService)
        {
            _dataService = dataService;
        }

        // GET: api/insights/{username}
        [HttpGet("{username}")]
        public async Task<IActionResult> DemandPrediction(FileInfo csvFile, string username)
        {
            // var insightJson = await _dataService.TrainDemandModelEndpoint(csvFile,username);

            // Return the JSON file of the insight
            // return Content(insightJson, "application/json");
            return null;
        }
    }
}