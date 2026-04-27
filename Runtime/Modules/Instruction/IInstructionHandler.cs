namespace Utility.Configurators
{
    public interface IInstructionHandler
    {
        void SetData(object data);
        void Apply();
    }
}
