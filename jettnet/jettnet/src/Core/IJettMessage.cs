using jettnet.mirage.bitpacking;

namespace jettnet
{
    public interface IJettMessage<T> : IJettMessage where T : struct
    {
        T Deserialize(JettReader reader);
    }

    public interface IJettMessage
    {
        void Serialize(JettWriter writer);
    }
}