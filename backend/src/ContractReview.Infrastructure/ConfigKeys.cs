namespace ContractReview.Infrastructure;

public static class ConfigKeys
{
    public const string AppDbConnection = "AppDb";
    public const string EfConnection = "EF_CONNECTION";
    public const string EfAllowNonLocal = "EF_ALLOW_NON_LOCAL";
    public const string EfAllowNonLocalEnabled = "1";
    public const string LocalDbServerPrefix = "(localdb)\\";
    public const string DevelopmentConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ContractReview_Dev;Trusted_Connection=True;TrustServerCertificate=True";
}
