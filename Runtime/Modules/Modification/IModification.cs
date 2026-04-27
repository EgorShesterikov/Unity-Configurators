namespace Utility.Configurators
{
    public interface IModification<in TContext>
    {
        void Apply(TContext context);
    }
}
