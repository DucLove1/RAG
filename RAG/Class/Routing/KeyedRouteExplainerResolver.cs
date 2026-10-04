using RAG.Class.Constants;
using RAG.Interface;

namespace RAG.Class.Routing
{
    /// <summary>
    /// Cài đặt <see cref="IRouteExplainerResolver"/> dựa trên Keyed Services.
    /// Nơi duy nhất của benchmark chạm tới service locator.
    /// </summary>
    public sealed class KeyedRouteExplainerResolver : IRouteExplainerResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public KeyedRouteExplainerResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IRouteExplainer Resolve(SemanticRouterStrategy strategy) =>
            _serviceProvider.GetRequiredKeyedService<IRouteExplainer>(strategy);
    }
}
