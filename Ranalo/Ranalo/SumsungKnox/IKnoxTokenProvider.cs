namespace Ranalo.SumsungKnox
{
    public interface IKnoxTokenProvider
    {
        Task<string> GetTokenAsync();
    }
}
