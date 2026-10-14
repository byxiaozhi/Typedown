using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation.TestHost
{
    /// <summary>
    /// One-shot barriers in the write path. A test arms one for a point (and optionally a document), waits until an
    /// edit reaches it, does what the race needs (types in the window, closes the tab, kills the web view), then
    /// releases it. A barrier nobody releases lets the edit go after <see cref="MaxHold"/>, so a failed test cannot
    /// hang the host.
    /// </summary>
    public sealed class EditBarriers : IEditBarriers
    {
        public static readonly string[] Points = { EditBarrierPoints.BeforeFlushReply, EditBarrierPoints.AfterEditorMutationBeforeReport, EditBarrierPoints.BeforeSaveCommit };

        public TimeSpan MaxHold { get; set; } = TimeSpan.FromSeconds(60);

        private sealed class Barrier
        {
            public string Id = Guid.NewGuid().ToString("N");
            public string Point = "";
            public string? DocumentId;
            public string? HitOperationId;
            public bool Hit;
            public readonly TaskCompletionSource<bool> Reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly object gate = new();
        private readonly List<Barrier> armed = new();
        private readonly Dictionary<string, Barrier> byId = new();

        public string Arm(string point, string? documentId)
        {
            if (!Points.Contains(point)) throw Params.Invalid("point", "unknownValue");
            var barrier = new Barrier { Point = point, DocumentId = documentId };
            lock (gate)
            {
                armed.Add(barrier);
                byId[barrier.Id] = barrier;
            }
            return barrier.Id;
        }

        public async Task PassAsync(string point, string documentId, string? operationId)
        {
            Barrier? barrier;
            lock (gate)
            {
                barrier = armed.FirstOrDefault(b => b.Point == point && (b.DocumentId == null || b.DocumentId == documentId));
                if (barrier == null) return;
                armed.Remove(barrier); // one-shot
                barrier.Hit = true;
                barrier.HitOperationId = operationId;
            }
            barrier.Reached.TrySetResult(true);
            await Task.WhenAny(barrier.Released.Task, Task.Delay(MaxHold));
        }

        private Barrier Get(string id)
        {
            lock (gate)
                return byId.TryGetValue(id, out var b) ? b : throw Params.Invalid("barrierId", "unknown");
        }

        /// <summary>True once an edit is held at the barrier; false when <paramref name="timeout"/> passed first.</summary>
        public async Task<(bool hit, string? operationId)> WaitHitAsync(string id, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var barrier = Get(id);
            var finished = await Task.WhenAny(barrier.Reached.Task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            return (finished == barrier.Reached.Task, barrier.HitOperationId);
        }

        public void Release(string id)
        {
            var barrier = Get(id);
            lock (gate)
            {
                armed.Remove(barrier);
                byId.Remove(id);
            }
            barrier.Released.TrySetResult(true);
        }

        /// <summary>
        /// The test.* methods. The application's method table refuses these names; only the test host adds them.
        /// </summary>
        public void AddMethods(MethodTable methods)
        {
            // Arms a one-shot barrier at a point of the write path (optionally for one document); returns its barrierId.
            methods.Add(new MethodDescriptor("test.barrier.arm", null, "test.barrier.arm/1", (c, _) =>
            {
                var p = c.Params;
                var id = Arm(p.RequiredString("point", allowEmpty: false), p.OptionalString("documentId"));
                return Task.FromResult<JToken?>(new JObject { ["barrierId"] = id });
            }));
            // Waits until an edit is held at the barrier (hit) or the timeout passes; the held operation's id.
            methods.Add(new MethodDescriptor("test.barrier.waitHit", null, "test.barrier.waitHit/1", async (c, ct) =>
            {
                var p = c.Params;
                var (hit, operationId) = await WaitHitAsync(p.RequiredString("barrierId"), TimeSpan.FromMilliseconds(p.OptionalInteger("timeoutMs", 0, 600000) ?? 10000), ct).ConfigureAwait(false);
                return new JObject { ["hit"] = hit, ["operationId"] = operationId };
            }));
            // Lets the held edit go on.
            methods.Add(new MethodDescriptor("test.barrier.release", null, "test.barrier.release/1", (c, _) =>
            {
                Release(c.Params.RequiredString("barrierId"));
                return Task.FromResult<JToken?>(new JObject());
            }));
        }
    }
}
