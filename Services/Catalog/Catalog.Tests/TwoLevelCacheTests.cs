using System.Diagnostics.Metrics;
using Catalog.Bll.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Catalog.Tests;

/// <summary>Дворівневий кеш без Redis: L2 — MemoryDistributedCache (або «зламаний» Redis).</summary>
public class TwoLevelCacheTests
{
    private sealed record Dto(long Id, string Title);

    private sealed class BrokenRedis : IDistributedCache
    {
        public int Calls;
        private Exception Fail() { Calls++; return new InvalidOperationException("Redis is down"); }
        public byte[]? Get(string key) => throw Fail();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Fail();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Fail();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Fail();
        public void Refresh(string key) => throw Fail();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Fail();
        public void Remove(string key) => throw Fail();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Fail();
    }

    private static (TwoLevelCache Cache, MemoryCache L1, IDistributedCache L2) Create(IDistributedCache? l2 = null)
    {
        var l1 = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });   // SizeLimit → кожен запис мусить мати Size
        l2 ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var meters = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        return (new TwoLevelCache(l1, l2, new CatalogCacheMetrics(meters), NullLogger<TwoLevelCache>.Instance), l1, l2);
    }

    [Fact]
    public async Task Cache_aside_reads_database_once_then_serves_from_L1()
    {
        var (cache, _, _) = Create();
        var dbCalls = 0;
        Task<Dto> Db(CancellationToken _) { dbCalls++; return Task.FromResult(new Dto(1, "Довбуш")); }

        await cache.GetOrCreateAsync(CacheKeys.Movie(1), CachePolicy.Movie, Db, default);
        var second = await cache.GetOrCreateAsync(CacheKeys.Movie(1), CachePolicy.Movie, Db, default);

        Assert.Equal(1, dbCalls);
        Assert.Equal("Довбуш", second.Title);
    }

    [Fact]
    public async Task L2_refills_L1_of_another_instance()
    {
        var shared = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var (instanceA, _, _) = Create(shared);
        var (instanceB, l1B, _) = Create(shared);                               // інший екземпляр сервісу, спільний Redis
        await instanceA.GetOrCreateAsync(CacheKeys.Movie(2), CachePolicy.Movie, _ => Task.FromResult(new Dto(2, "Мавка")), default);

        var fromB = await instanceB.GetOrCreateAsync<Dto>(CacheKeys.Movie(2), CachePolicy.Movie,
            _ => throw new InvalidOperationException("database must not be called"), default);

        Assert.Equal("Мавка", fromB.Title);
        Assert.True(l1B.TryGetValue(CacheKeys.Movie(2), out _));                 // зворотний шлях L2 → L1
    }

    [Fact]
    public async Task Invalidation_removes_both_levels()
    {
        var (cache, l1, l2) = Create();
        await cache.GetOrCreateAsync(CacheKeys.Movie(3), CachePolicy.Movie, _ => Task.FromResult(new Dto(3, "Дюна")), default);

        await cache.InvalidateAsync("MoviePriceChanged", default, CacheKeys.Movie(3));

        Assert.False(l1.TryGetValue(CacheKeys.Movie(3), out _));
        Assert.Null(await l2.GetAsync(CacheKeys.Movie(3)));
    }

    [Fact]
    public async Task Redis_down_falls_back_to_database_and_stops_calling_it()
    {
        var broken = new BrokenRedis();
        var (cache, _, _) = Create(broken);

        var a = await cache.GetOrCreateAsync(CacheKeys.Movie(4), CachePolicy.Movie, _ => Task.FromResult(new Dto(4, "A")), default);
        var b = await cache.GetOrCreateAsync(CacheKeys.Movie(5), CachePolicy.Movie, _ => Task.FromResult(new Dto(5, "B")), default);

        Assert.Equal("A", a.Title);
        Assert.Equal("B", b.Title);
        Assert.Equal(1, broken.Calls);                                          // після першої помилки L2 оминається
    }

    [Fact]
    public void L1_ttl_is_shorter_than_L2_for_every_policy()
    {
        foreach (var policy in new[] { CachePolicy.Movie, CachePolicy.Showtime, CachePolicy.Reference })
            Assert.True(policy.L1Absolute < policy.L2Absolute, policy.Name);
    }
}
