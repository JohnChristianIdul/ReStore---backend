using CsvHelper;
using ExcelDataReader;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;
using System.Globalization;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InsightController : ControllerBase
    {
        private readonly IDataService _dataService;

        // Constructor injection of IDataService
        public InsightController(IDataService dataService)
        {
            _dataService = dataService;
        }

        // Action to generate sales insight
        [HttpPost("/sales/insight")]
        public async Task<IActionResult> GenerateSalesInsight(IFormFile file, [FromForm] string username)
        {
            // Check the file extension
            string extension = Path.GetExtension(file.FileName).ToLower();

            if (extension != ".csv" && extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest("Unsupported file format. Please upload a CSV or Excel file.");
            }

            // Convert file to MemoryStream
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);

                // Reset the stream position to the beginning before processing
                memoryStream.Position = 0;

                if (extension == ".csv")
                {
                    // Directly pass CSV memory stream
                    return await ProcessCsv(memoryStream, username);
                }
                else
                {
                    // Convert Excel to CSV and pass it
                    using (var csvMemoryStream = ConvertExcelToCsv(memoryStream))
                    {
                        return await ProcessCsv(csvMemoryStream, username);
                    }
                }
            }
        }

        // Process the CSV memory stream and generate insight
        private async Task<IActionResult> ProcessCsv(MemoryStream memoryStream, string username)
        {
            try
            {
                // Call GenerateSalesInsight in DataService
                string insights = await _dataService.SalesInsight(memoryStream, username);

                // Return insights as a response
                return Ok(new { insights });
            }
            catch (Exception ex)
            {
                // Handle exceptions and return an appropriate error response
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Convert Excel file (MemoryStream) to CSV (MemoryStream)
        private MemoryStream ConvertExcelToCsv(MemoryStream excelStream)
        {
            var csvStream = new MemoryStream();

            try
            {
                // Initialize Excel reader
                using (var reader = ExcelReaderFactory.CreateReader(excelStream))
                {
                    // Read the first sheet
                    if (reader.Read())
                    {
                        // Create a DataTable to hold the data
                        var dataTable = new DataTable();

                        // Add columns to DataTable
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            dataTable.Columns.Add(reader.GetString(i));
                        }

                        // Add rows to DataTable
                        while (reader.Read())
                        {
                            var row = dataTable.NewRow();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[i] = reader.GetValue(i);
                            }
                            dataTable.Rows.Add(row);
                        }

                        // Write DataTable to CSV
                        using (var writer = new StreamWriter(csvStream, leaveOpen: true))
                        using (var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)))
                        {
                            // Write the header
                            foreach (DataColumn column in dataTable.Columns)
                            {
                                csv.WriteField(column.ColumnName);
                            }
                            csv.NextRecord();

                            // Write the rows
                            foreach (DataRow dataRow in dataTable.Rows)
                            {
                                foreach (var item in dataRow.ItemArray)
                                {
                                    csv.WriteField(item);
                                }
                                csv.NextRecord();
                            }
                        }

                        // Reset the stream position to the beginning
                        csvStream.Position = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions
                throw new InvalidOperationException("Error converting Excel to CSV.", ex);
            }

            return csvStream;
        }
    }
}