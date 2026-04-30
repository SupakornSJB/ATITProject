using System.Collections.Generic;

namespace IoTGame.Models
{
    public record IoTInputEvent(
        string PlayerId,
        string DeviceId,
        string InputName,
        string InputType,
        string EventType,
        float Value,
        long Timestamp,
        long SequenceNumber,
        IReadOnlyDictionary<string, float> Auxiliary);
}
