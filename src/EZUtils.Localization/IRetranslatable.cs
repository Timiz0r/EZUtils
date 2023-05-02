namespace EZUtils.Localization
{
    public interface IRetranslatable
    {
        void Retranslate();
        bool IsFinished { get; }
    }
}
