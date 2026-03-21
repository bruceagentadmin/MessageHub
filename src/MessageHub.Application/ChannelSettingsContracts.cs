using MessageHub.Domain;

namespace MessageHub.Application;

public interface IChannelSettingsStore
{
    Task<ChannelSettingsDocument> LoadAsync(CancellationToken cancellationToken = default);
    Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default);
    string GetFilePath();
}

public interface IChannelSettingsService
{
    Task<ChannelSettingsDocument> GetAsync(CancellationToken cancellationToken = default);
    Task<ChannelSettingsDocument> SaveAsync(ChannelSettingsDocument document, CancellationToken cancellationToken = default);
    IReadOnlyList<ChannelTypeDefinition> GetChannelTypes();
    string GetSettingsFilePath();
}
