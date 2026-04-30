using System.Collections.Generic;

namespace IoTGame.Models
{
    public record EnvironmentEvent(
        string EnvKey,
        float RawValue,
        long Timestamp,
        IReadOnlyDictionary<string, float> Auxiliary);
}
