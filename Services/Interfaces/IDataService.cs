namespace ReStore___backend.Services.Interfaces
{
    public interface IDataService
    {
        Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string username); 
        Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username);
        Task<string> TrainDemandModelEndpoint();
        Task<string> PredictDemandEndpoint();
        Task<string> SignUp(string email, string name, string password, string phoneNumber, string username);
        Task<string> Login(string email, string password);
    }
}
