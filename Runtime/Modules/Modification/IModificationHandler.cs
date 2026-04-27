namespace Utility.Configurators
{
    public interface IModificationHandler { }
    
    public interface IModificationHandler<in TContext> : IModificationHandler
    {
        void SetData(object data);
        void Apply(TContext context);
    }
}