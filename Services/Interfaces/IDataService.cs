namespace ReStore___backend.Services.Interfaces
{
    public interface IDataService
    {
        List<dynamic> ProcessData(IEnumerable<dynamic> records);
        Task SaveDataToFirestore(List<dynamic> processedData);
        Task<string> TrainDemandModelEndpoint();
        Task<string> PredictDemandEndpoint();
    }
}
