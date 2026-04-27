namespace Utility.Configurators
{
    public abstract class ModificationHandler<TData, TContext> : IModificationHandler<TContext> where TData : class
    {
        protected TData Data { get; private set; }

        public void SetData(object data) => Data = data as TData;
        
        public abstract void Apply(TContext context);
    }
}