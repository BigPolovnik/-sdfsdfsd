using MoviePlatform.Domain;

namespace MoviePlatform.Application;

public interface IMediaBalancer
{
    Task<IReadOnlyCollection<MediaSource>> GetSourcesAsync(
        int contentId,
        CancellationToken cancellationToken = default);
}

public sealed class MediaBalancerRegistry
{
    private readonly IEnumerable<IMediaBalancer> _balancers;
    public MediaBalancerRegistry(IEnumerable<IMediaBalancer> balancers) => _balancers = balancers;
    public IEnumerable<IMediaBalancer> All => _balancers;
}

// Placeholder until the authorized balancer API is provided.
public sealed class PlaceholderBalancer : IMediaBalancer
{
    public Task<IReadOnlyCollection<MediaSource>> GetSourcesAsync(
        int contentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<MediaSource>>([]);
}
