using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Models;

namespace Typedown.Universal.Services
{
    public class FileHistory
    {
        public ObservableCollection<string> RecentlyOpened { get; } = new();

        public IServiceProvider ServiceProvider { get; }

        public FileHistory(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _ = UpdateRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
        }

        public async Task RecordHistory(string filePath)
        {
            using var ctx = await ServiceProvider.GetAppDbContext();
            var model = ctx.FileAccessHistories;
            var item = new FileAccessHistory() { FilePath = filePath, AccessTime = DateTime.Now };
            await model.AddAsync(item);
            await ctx.SaveChangesAsync();
            await UpdateRecentlyOpened(filePath, CollectionChangeAction.Add);
        }

        public async Task RemoveHistory(string filePath)
        {
            using var ctx = await ServiceProvider.GetAppDbContext();
            var model = ctx.FileAccessHistories;
            model.RemoveRange(model.Where(x => x.FilePath == filePath));
            await ctx.SaveChangesAsync();
            await UpdateRecentlyOpened(filePath, CollectionChangeAction.Remove);
        }

        public async Task ClearHistory()
        {
            using var ctx = await ServiceProvider.GetAppDbContext();
            var model = ctx.FileAccessHistories;
            model.RemoveRange(model);
            await ctx.SaveChangesAsync();
            await UpdateRecentlyOpened(string.Empty, CollectionChangeAction.Refresh);
        }

        private async Task UpdateRecentlyOpened(string filePath, CollectionChangeAction action)
        {
            using var ctx = await ServiceProvider.GetAppDbContext();
            var model = ctx.FileAccessHistories;
            var maxCount = 10;
            switch (action)
            {
                case CollectionChangeAction.Add:
                    RecentlyOpened.Remove(filePath);
                    RecentlyOpened.Insert(0, filePath);
                    break;
                case CollectionChangeAction.Remove:
                    RecentlyOpened.Remove(filePath);
                    break;
                case CollectionChangeAction.Refresh:
                    RecentlyOpened.Clear();
                    break;
            }
            while (RecentlyOpened.Count > maxCount)
            {
                RecentlyOpened.RemoveAt(RecentlyOpened.Count - 1);
            }
            if (RecentlyOpened.Count < maxCount)
            {
                await foreach (var item in model.OrderByDescending(x => x.AccessTime).AsAsyncEnumerable())
                {
                    if (!RecentlyOpened.Contains(item.FilePath))
                        RecentlyOpened.Add(item.FilePath);
                }
            }
        }
    }
}
