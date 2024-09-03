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


    }
}
