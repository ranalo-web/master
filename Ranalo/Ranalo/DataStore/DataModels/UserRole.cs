namespace Ranalo.DataStore.DataModels
{
    public enum UserRole
    {
        Guest,
        Admin,
        Dealer,
        Customer,
        Supplier,
        Approver,
        Collector,
        Agent
    }

    public enum UserStatus
    {
        None,
        Active,
        Pending,
        Inactive,
        Suspended,
        Deleted
    }
}
