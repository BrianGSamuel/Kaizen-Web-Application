namespace KaizenWebApp.Enums
{
    public enum KaizenStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public enum ApprovalType
    {
        Engineer,
        Manager
    }

    public enum CostSavingType
    {
        NoCostSaving,
        HasCostSaving
    }

    public enum AwardPrice
    {
        FirstPrice,
        SecondPrice,
        ThirdPrice,
        NoPrice
    }

    public enum UserRole
    {
        Admin,
        User,
        Manager,
        Engineer,
        KaizenTeam
    }
}
