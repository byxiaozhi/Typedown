using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Models;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    public class AccessHistory
    {
        public ObservableCollection<string> FileRecentlyOpened { get; } = new();

        public ObservableCollection<string> FolderRecentlyOpened { get; } = new();

        private readonly TaskCompletionSource<bool> initializedTask = new();

        // Serialise record writes: two concurrent DbContexts could each find no row and both insert, duplicating a
        // path. This closes the in-process race (a cross-process unique index/migration is still TODO).
        private static readonly System.Threading.SemaphoreSlim recordLock = new(1, 1);

        public AccessHistory()
        {
            _ = UpdateRecentlyOpened();
        }

        public async Task RecordFileHistory(string filePath)
        {
            await recordLock.WaitAsync();
            try
            {
                using var ctx = await AppDbContext.Create();
                var model = ctx.FileAccessHistories;
                // One row per path, its time updated — not a new row every save. Otherwise the table grows without
                // bound and the most-recent query returns many rows of the same file, crowding others out.
                var existing = await model.FirstOrDefaultAsync(x => x.FilePath == filePath);
                if (existing != null)
                    existing.AccessTime = DateTime.Now;
                else
                    await model.AddAsync(new FileAccessHistory() { FilePath = filePath, AccessTime = DateTime.Now });
                await ctx.SaveChangesAsync();
            }
            finally
            {
                recordLock.Release();
            }
            await UpdateFileRecentlyOpened(filePath, CollectionChangeAction.Add);
        }

        public async Task RemoveFileHistory(string filePath)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FileAccessHistories;
            model.RemoveRange(model.Where(x => x.FilePath == filePath));
            await ctx.SaveChangesAsync();
            await UpdateFileRecentlyOpened(filePath, CollectionChangeAction.Remove);
        }

        public async Task ClearFileHistory()
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FileAccessHistories;
            model.RemoveRange(model);
            await ctx.SaveChangesAsync();
            await UpdateFileRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
        }

        private async Task UpdateFileRecentlyOpened(string filePath, CollectionChangeAction action)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FileAccessHistories;
            var maxCount = 10;
            switch (action)
            {
                case CollectionChangeAction.Add:
                    FileRecentlyOpened.Remove(filePath);
                    FileRecentlyOpened.Insert(0, filePath);
                    break;
                case CollectionChangeAction.Remove:
                    FileRecentlyOpened.Remove(filePath);
                    break;
                case CollectionChangeAction.Refresh:
                    FileRecentlyOpened.Clear();
                    break;
            }
            while (FileRecentlyOpened.Count > maxCount)
            {
                FileRecentlyOpened.RemoveAt(FileRecentlyOpened.Count - 1);
            }
            if (FileRecentlyOpened.Count < maxCount)
            {
                await foreach (var item in model.OrderByDescending(x => x.AccessTime).Take(maxCount).AsAsyncEnumerable())
                {
                    if (!FileRecentlyOpened.Contains(item.FilePath))
                        FileRecentlyOpened.Add(item.FilePath);
                }
            }
        }

        public async Task RecordFolderHistory(string folderPath)
        {
            await recordLock.WaitAsync();
            try
            {
                using var ctx = await AppDbContext.Create();
                var model = ctx.FolderAccessHistories;
                // One row per path, its time updated — see RecordFileHistory.
                var existing = await model.FirstOrDefaultAsync(x => x.FolderPath == folderPath);
                if (existing != null)
                    existing.AccessTime = DateTime.Now;
                else
                    await model.AddAsync(new FolderAccessHistory() { FolderPath = folderPath, AccessTime = DateTime.Now });
                await ctx.SaveChangesAsync();
            }
            finally
            {
                recordLock.Release();
            }
            await UpdateFolderRecentlyOpened(folderPath, CollectionChangeAction.Add);
        }

        public async Task RemoveFolderHistory(string folderPath)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FolderAccessHistories;
            model.RemoveRange(model.Where(x => x.FolderPath == folderPath));
            await ctx.SaveChangesAsync();
            await UpdateFolderRecentlyOpened(folderPath, CollectionChangeAction.Remove);
        }

        public async Task ClearFolderHistory()
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FolderAccessHistories;
            model.RemoveRange(model);
            await ctx.SaveChangesAsync();
            await UpdateFolderRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
        }

        private async Task UpdateFolderRecentlyOpened(string folderPath, CollectionChangeAction action)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.FolderAccessHistories;
            var maxCount = 10;
            switch (action)
            {
                case CollectionChangeAction.Add:
                    FolderRecentlyOpened.Remove(folderPath);
                    FolderRecentlyOpened.Insert(0, folderPath);
                    break;
                case CollectionChangeAction.Remove:
                    FolderRecentlyOpened.Remove(folderPath);
                    break;
                case CollectionChangeAction.Refresh:
                    FolderRecentlyOpened.Clear();
                    break;
            }
            while (FolderRecentlyOpened.Count > maxCount)
            {
                FolderRecentlyOpened.RemoveAt(FolderRecentlyOpened.Count - 1);
            }
            if (FolderRecentlyOpened.Count < maxCount)
            {
                await foreach (var item in model.OrderByDescending(x => x.AccessTime).Take(maxCount).AsAsyncEnumerable())
                {
                    if (!FolderRecentlyOpened.Contains(item.FolderPath))
                        FolderRecentlyOpened.Add(item.FolderPath);
                }
            }
        }

        private async Task UpdateRecentlyOpened()
        {
            try
            {
                var updateFileTask = UpdateFileRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
                var updateFolderTask = UpdateFolderRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
                await Task.WhenAll(updateFileTask, updateFolderTask);
            }
            catch (Exception ex)
            {
                // A failed history load (a locked or corrupt database, a migration error) must not leave
                // EnsureInitialized awaiting forever — start-up waits on it. Report it and carry on empty.
                Log.Debug($"AccessHistory init failed: {ex.Message}");
            }
            finally
            {
                initializedTask.TrySetResult(true);
            }
        }

        public async Task EnsureInitialized()
        {
            await initializedTask.Task;
        }

        public async Task ClearHistory()
        {
            await ClearFileHistory();
            await ClearFolderHistory();
        }
    }
}
