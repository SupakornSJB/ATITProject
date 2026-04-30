using System.Collections.Generic;

namespace IoTGame.Models
{
    public record AdminCommand(
        string Command,
        string IssuedBy,
        long Timestamp,
        IReadOnlyDictionary<string, float> Auxiliary);
}
