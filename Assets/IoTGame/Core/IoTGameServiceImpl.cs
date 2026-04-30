#if UNITY_SERVER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameController.V2;
using Grpc.Core;
using IoTGame.Models;

namespace IoTGame.Core
{
    public readonly struct InputBatchEnvelope
    {
        public readonly List<InputEventRequest> Events;
        public InputBatchEnvelope(List<InputEventRequest> events) => Events = events;
    }

    // Never touch the Unity API from inside this class — all calls come in on Kestrel
    // background threads. Enqueue only; managers drain the queues on the main thread.
    public class IoTGameServiceImpl : GameControllerService.GameControllerServiceBase
    {
        public static readonly ConcurrentQueue<InputBatchEnvelope> InputQueue    = new();
        public static readonly ConcurrentQueue<EnvironmentEvent>   EnvironmentQueue = new();
        public static readonly ConcurrentQueue<AdminCommand>       AdminCommandQueue = new();

        private static readonly IReadOnlyDictionary<string, float> EmptyAuxiliary =
            new Dictionary<string, float>();

        private void ValidateSecret(ServerCallContext context)
        {
            string secret = IoTGame.Core.IoTGameServer.SharedSecret;
            if (string.IsNullOrEmpty(secret)) return; // secret not configured — allow all
            string provided = context.RequestHeaders.GetValue("x-iotgame-secret");
            if (provided != secret)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid shared secret"));
        }

        public override Task<InputEventResponse> SendInputEvent(
            InputEventRequest request, ServerCallContext context)
        {
            ValidateSecret(context);
            RouteEvent(request);
            Console.WriteLine($"[IoTGameServiceImpl] InputEvent — player={request.PlayerId} input={request.InputName} type={request.EventType} value={request.Value}");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        public override Task<InputEventResponse> SendEnvironmentEvent(
            EnvironmentEventRequest request, ServerCallContext context)
        {
            ValidateSecret(context);
            EnvironmentQueue.Enqueue(new EnvironmentEvent(
                request.EnvKey,
                request.RawValue,
                request.Timestamp,
                EmptyAuxiliary));
            Console.WriteLine($"[IoTGameServiceImpl] EnvironmentEvent — key={request.EnvKey} value={request.RawValue}");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        public override Task<InputEventResponse> SendInputBatch(
            InputBatchRequest request, ServerCallContext context)
        {
            ValidateSecret(context);
            var gameEvents = new List<InputEventRequest>(request.Events.Count);
            foreach (var evt in request.Events)
            {
                if (evt.InputType == "environment")
                    EnvironmentQueue.Enqueue(new EnvironmentEvent(evt.InputName, evt.Value, evt.Timestamp, EmptyAuxiliary));
                else if (evt.InputType == "admin")
                    AdminCommandQueue.Enqueue(new AdminCommand(evt.InputName, evt.PlayerId, evt.Timestamp, EmptyAuxiliary));
                else
                    gameEvents.Add(evt);
            }
            if (gameEvents.Count > 0)
                InputQueue.Enqueue(new InputBatchEnvelope(gameEvents));

            Console.WriteLine($"[IoTGameServiceImpl] InputBatch — {request.Events.Count} events");
            return Task.FromResult(new InputEventResponse { Accepted = true, Message = "ok" });
        }

        private static void RouteEvent(InputEventRequest request)
        {
            if (request.InputType == "environment")
                EnvironmentQueue.Enqueue(new EnvironmentEvent(request.InputName, request.Value, request.Timestamp, EmptyAuxiliary));
            else if (request.InputType == "admin")
                AdminCommandQueue.Enqueue(new AdminCommand(request.InputName, request.PlayerId, request.Timestamp, EmptyAuxiliary));
            else
                InputQueue.Enqueue(new InputBatchEnvelope(new List<InputEventRequest> { request }));
        }
    }
}
#endif
