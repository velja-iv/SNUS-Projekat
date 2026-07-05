using System.Collections.Concurrent;

namespace NotificationService.Services;

public sealed class SensorConnectionRegistry
{
    private readonly ConcurrentDictionary<string, string> _connections = new(StringComparer.Ordinal);

    public void Register(string sensorId, string connectionId)
    {
        _connections[sensorId] = connectionId;
    }

    public bool TryGetConnectionId(string sensorId, out string connectionId)
    {
        return _connections.TryGetValue(sensorId, out connectionId!);
    }

    public string? RemoveByConnectionId(string connectionId)
    {
        var item = _connections.FirstOrDefault(x => string.Equals(x.Value, connectionId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(item.Key))
        {
            return null;
        }

        _connections.TryRemove(item.Key, out _);
        return item.Key;
    }
}
