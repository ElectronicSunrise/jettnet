using System.Threading;

namespace JamesFrowen.SimpleWeb
{
    internal static class Utils
    {
        public static void CheckForInterrupt()
        {
            // sleep in order to check for ThreadInterruptedException
            Thread.Sleep(1);
        }
    }
}
