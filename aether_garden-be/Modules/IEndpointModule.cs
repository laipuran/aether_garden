using aether_garden_be.Options;

namespace aether_garden_be.Modules;

public interface IEndpointModule
{
    bool IsEnabled(FeatureOptions features);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
