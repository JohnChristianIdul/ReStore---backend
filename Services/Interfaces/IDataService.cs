using ReStore___backend.Dtos;

namespace ReStore___backend.Services.Interfaces
{
    public interface IDataService
    {
        Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string username); 
        Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username);
        Task<string> SalesInsight(MemoryStream salesData, string username);
        Task<string> TrainDemandModelEndpoint();
        Task<string> PredictDemandEndpoint();
        Task<string> SignUp(string email, string name, string username, string phoneNumber, string password);
        Task<LoginResultDTO> Login(string email, string password);
    }
}
