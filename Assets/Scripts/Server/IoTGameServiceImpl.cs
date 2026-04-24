#if UNITY_SERVER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameController.V2;
using Grpc.Core;

namespace Server
{
    public readonly struct InputBatchEnvelope
    {
        public readonly List<InputEventRequest> Events;

        public InputBatchEnvelope(List<InputEventRequest> events)
        {
            Events = events;
        }
    }

    // Never touch the Unity API from inside this class — all calls come in on Kestrel
    // background threads. Enqueue only; IoTInputManager drains the queue on the main thread.
    public class IoTGameServiceImpl : GameControllerService.GameControllerServiceBase
    {
        public static readonly ConcurrentQueue<InputBatchEnvelope> InputQueue = new();

        public override Task<InputEventResponse> SendInputEvent(
            InputEventRequest request, ServerCallContext context)
        {
            InputQueue.Enqueue(new InputBatchEnvelope(new List<InputEventRequest> { request }));
            Console.WriteLine($"[IoTGameServiceImpl] Received InputEvent — playerId={request.PlayerId} input={request.InputName} type={request.EventType} value={request.Value}");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        public override Task<InputEventResponse> SendInputBatch(
            InputBatchRequest request, ServerCallContext context)
        {
            InputQueue.Enqueue(new InputBatchEnvelope(new List<InputEventRequest>(request.Events)));
            Console.WriteLine($"[IoTGameServiceImpl] Received InputBatch — {request.Events.Count} events");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }
    }
}
#endif