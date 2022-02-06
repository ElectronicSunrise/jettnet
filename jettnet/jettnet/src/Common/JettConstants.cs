namespace jettnet
{
    public class JettConstants
    {
        public const  int DefaultBufferSize = 1200 - OVERHEAD;
        private const int OVERHEAD          = sizeof(int);

        public const int ReliableChannel   = 0;
        public const int UnreliableChannel = 1;
    }
}