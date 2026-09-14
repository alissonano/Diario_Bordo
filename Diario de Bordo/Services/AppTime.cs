namespace Diario_de_Bordo.Services
{
    public static class AppTime
    {
        private static readonly TimeZoneInfo ManausZone =
            TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");

        // Use sempre AppTime.Now em vez de DateTime.Now
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ManausZone);
    }
}
