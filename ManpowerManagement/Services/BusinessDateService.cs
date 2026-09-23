namespace ManpowerManagement.Services;

public static class BusinessDateService
{
    public static DateOnly Current(DateTime? localNow = null)
    {
        var now = localNow ?? DateTime.Now;
        return DateOnly.FromDateTime(now.Hour < 7 ? now.AddDays(-1) : now);
    }
}
