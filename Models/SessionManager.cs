// File: Helpers/SessionManager.cs
namespace JNR.Helpers
{
    public static class SessionManager
    {
        public static int? CurrentUserId { get; set; }
        // You could also store the Username if needed for display purposes elsewhere
        public static string CurrentUsername { get; set; }
    }
}