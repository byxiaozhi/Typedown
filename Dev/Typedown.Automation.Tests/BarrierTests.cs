using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Typedown.Automation.TestHost;
using Xunit;

namespace Typedown.Automation.Tests
{
    public class BarrierTests
    {
        private static EditRequest Replace(long baseRevision, string text, bool save = false) => new() { BaseRevision = baseRevision, Edit = _ => text, Save = save };

        [Fact]
        public async Task Typing_while_held_after_the_flush_is_caught_by_the_page()
        {
            var barriers = new EditBarriers();
            var coordinator = new DocumentEditCoordinator(2, barriers);
            var doc = new FakeDocument();
            var id = barriers.Arm(EditBarrierPoints.BeforeFlushReply, doc.DocumentId);
            var edit = coordinator.EditAsync(doc, Replace(0, "x\n"), CancellationToken.None);
            var (hit, _) = await barriers.WaitHitAsync(id, TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.True(hit);
            doc.PageText += "typed while held";
            barriers.Release(id);
            var e = await Assert.ThrowsAsync<AutomationException>(() => edit);
            Assert.Equal(AutomationErrorKind.revision_conflict, e.Kind);
            Assert.Equal("# base\ntyped while held", doc.PageText);
        }

        [Fact]
        public async Task Held_after_the_page_applied_nothing_is_committed_until_released()
        {
            var barriers = new EditBarriers();
            var coordinator = new DocumentEditCoordinator(2, barriers);
            var doc = new FakeDocument();
            var id = barriers.Arm(EditBarrierPoints.AfterEditorMutationBeforeReport, null);
            var edit = coordinator.EditAsync(doc, Replace(0, "x\n"), CancellationToken.None);
            var (hit, operationId) = await barriers.WaitHitAsync(id, TimeSpan.FromSeconds(10), CancellationToken.None);
            Assert.True(hit);
            Assert.Equal(32, operationId!.Length);
            Assert.Equal("x\n", doc.PageText);
            Assert.Equal(("# base\n", 0L), (doc.Text, doc.Revision));
            barriers.Release(id);
            var result = await edit;
            Assert.Equal(operationId, result.OperationId);
            Assert.Equal(1, doc.Revision);
        }

        [Fact]
        public async Task Held_before_the_save_the_edit_is_committed_but_not_saved()
        {
            var barriers = new EditBarriers();
            var coordinator = new DocumentEditCoordinator(2, barriers);
            var doc = new FakeDocument();
            var id = barriers.Arm(EditBarrierPoints.BeforeSaveCommit, doc.DocumentId);
            var edit = coordinator.EditAsync(doc, Replace(0, "x\n", save: true), CancellationToken.None);
            Assert.True((await barriers.WaitHitAsync(id, TimeSpan.FromSeconds(10), CancellationToken.None)).hit);
            Assert.Equal(("x\n", 1L, false), (doc.Text, doc.Revision, doc.Saved));
            barriers.Release(id);
            Assert.True((await edit).Saved);
        }

        [Fact]
        public async Task Barriers_are_one_shot_scoped_to_their_document_and_never_hold_forever()
        {
            var barriers = new EditBarriers { MaxHold = TimeSpan.FromMilliseconds(200) };
            var coordinator = new DocumentEditCoordinator(2, barriers);
            var other = new FakeDocument();
            var doc = new FakeDocument();
            var id = barriers.Arm(EditBarrierPoints.BeforeFlushReply, doc.DocumentId);
            await coordinator.EditAsync(other, Replace(0, "o\n"), CancellationToken.None); // not this barrier's document
            Assert.False((await barriers.WaitHitAsync(id, TimeSpan.FromMilliseconds(50), CancellationToken.None)).hit);
            await coordinator.EditAsync(doc, Replace(0, "a\n"), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); // released by MaxHold
            Assert.True((await barriers.WaitHitAsync(id, TimeSpan.Zero, CancellationToken.None)).hit);
            await coordinator.EditAsync(doc, Replace(1, "b\n"), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)); // disarmed
            Assert.Throws<AutomationException>(() => barriers.Arm("somewhere", null));
        }

        [Fact]
        public void Test_methods_exist_only_in_the_test_host()
        {
            var barriers = new EditBarriers();
            Assert.Throws<InvalidOperationException>(() => barriers.AddMethods(new MethodTable(BuildTypes.Application)));
            var table = new MethodTable(BuildTypes.AutomationTestHost);
            barriers.AddMethods(table);
            Assert.True(table.TryGet("test.barrier.arm", out _));
        }

        [Fact]
        public async Task The_barrier_methods_work_over_a_connection()
        {
            var barriers = new EditBarriers();
            var table = Harness.StandardMethods(BuildTypes.AutomationTestHost);
            barriers.AddMethods(table);
            await using var h = new Harness(table, BuildTypes.AutomationTestHost);
            var init = await h.InitializeAsync();
            Assert.Equal("automationTestHost", (string)init["result"]!["server"]!["buildType"]!);
            var armed = await h.CallAsync("test.barrier.arm", new JObject { ["point"] = "beforeSaveCommit" });
            var barrierId = (string)armed["result"]!["barrierId"]!;
            var wait = await h.CallAsync("test.barrier.waitHit", new JObject { ["barrierId"] = barrierId, ["timeoutMs"] = 10 });
            Assert.False((bool)wait["result"]!["hit"]!);
            Assert.NotNull((await h.CallAsync("test.barrier.release", new JObject { ["barrierId"] = barrierId }))["result"]);
            var bad = await h.CallAsync("test.barrier.arm", new JObject { ["point"] = "nowhere" });
            Assert.Equal("invalid_params", (string)bad["error"]!["data"]!["kind"]!);
        }
    }
}
