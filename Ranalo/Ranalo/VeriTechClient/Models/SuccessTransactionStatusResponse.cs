namespace Ranalo.VeriTechClient.Models
{
    public class SuccessTransactionStatusResponse
    {
        public TransactionStatusResultModel Data { get; set; }
        public string Message { get; set; }
    }

    public class TransactionStatusResultModel
    {
        public string Status { get; set; }
        public string Transaction_Id { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }
}
