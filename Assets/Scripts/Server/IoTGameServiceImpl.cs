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
        public InputBatchEnvelope(List<InputEventRequest> events) => Events = events;
    }

    public struct EnvironmentEvent
    {
        public string EnvKey;
        public float RawValue;
        public long Timestamp;
    }

    // Never touch the Unity API from inside this class — all calls come in on Kestrel
    // background threads. Enqueue only; managers drain the queues on the main thread.
    public class IoTGameServiceImpl : GameControllerService.GameControllerServiceBase
    {
        public static readonly ConcurrentQueue<InputBatchEnvelope> InputQueue = new();
        public static readonly ConcurrentQueue<EnvironmentEvent> EnvironmentQueue = new();
        public static readonly ConcurrentQueue<string> AdminCommandQueue = new();

        public override Task<InputEventResponse> SendInputEvent(
            InputEventRequest request, ServerCallContext context)
        {
            RouteEvent(request);
            Console.WriteLine($"[IoTGameServiceImpl] InputEvent — player={request.PlayerId} input={request.InputName} type={request.EventType} value={request.Value}");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        public override Task<InputEventResponse> SendInputBatch(
            InputBatchRequest request, ServerCallContext context)
        {
            var gameEvents = new List<InputEventRequest>(request.Events.Count);
            foreach (var evt in request.Events)
            {
                if (evt.InputType == "environment")
                    EnvironmentQueue.Enqueue(new EnvironmentEvent { EnvKey = evt.InputName, RawValue = evt.Value, Timestamp = evt.Timestamp });
                else if (evt.InputType == "admin")
                    AdminCommandQueue.Enqueue(evt.InputName);
                else
                    gameEvents.Add(evt);
            }
            if (gameEvents.Count > 0)
                InputQueue.Enqueue(new InputBatchEnvelope(gameEvents));

            Console.WriteLine($"[IoTGameServiceImpl] InputBatch — {request.Events.Count} events");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        // Routes a single event: environment/admin events go to their own queues,
        // game events go to InputQueue.
        private static void RouteEvent(InputEventRequest request)
        {
            if (request.InputType == "environment")
                EnvironmentQueue.Enqueue(new EnvironmentEvent { EnvKey = request.InputName, RawValue = request.Value, Timestamp = request.Timestamp });
            else if (request.InputType == "admin")
                AdminCommandQueue.Enqueue(request.InputName);
            else
                InputQueue.Enqueue(new InputBatchEnvelope(new List<InputEventRequest> { request }));
        }
    }
}
#endif
