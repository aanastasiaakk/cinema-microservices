using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

/// <summary>Pipeline MediatR без бази: репозиторій підмінено фейком, що рахує виклики.</summary>
public class PipelineTests
{
    private sealed class FakeRepository : IReviewRepository
    {
        public int AddCalls;
        public bool AlreadyReviewed;
        public Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default) => Task.FromResult<Review?>(null);
        public Task<bool> ExistsAsync(long movieId, long customerId, CancellationToken ct = default) => Task.FromResult(AlreadyReviewed);
        public Task AddAsync(Review review, CancellationToken ct = default) { AddCalls++; return Task.CompletedTask; }
        public Task UpdateAsync(Review review, int expectedVersion, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> RenameMovieAsync(long movieId, string movieTitle, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private static (ISender Sender, FakeRepository Repo, IServiceProvider Sp) Build()
    {
        var repo = new FakeRepository();
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .AddSingleton<IReviewRepository>(repo);
        var config = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddApplication(config);
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<ISender>(), repo, sp);
    }

    private static CreateReviewCommand Valid(int rating = 5) =>
        new("viewer", 1, "Довбуш", 7, "Олена", rating, "Чудово");

    [Fact]
    public async Task Rating_10_is_rejected_before_the_handler()
    {
        var (sender, repo, _) = Build();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Valid(rating: 10)));
        Assert.Contains(ex.Errors, e => e.PropertyName == "Rating");
        Assert.Equal(0, repo.AddCalls);                                          // обробник не викликався
    }

    [Fact]
    public async Task All_errors_are_collected_together()
    {
        var (sender, _, _) = Build();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sender.Send(new CreateReviewCommand("bad", 0, "", 0, "", 0, null)));
        Assert.True(ex.Errors.Select(e => e.PropertyName).Distinct().Count() >= 5);
    }

    [Fact]
    public async Task Async_rule_rejects_second_viewer_review()
    {
        var (sender, repo, _) = Build();
        repo.AlreadyReviewed = true;
        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Valid()));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("already reviewed"));
        Assert.Equal(0, repo.AddCalls);
    }

    [Fact]
    public async Task Valid_command_reaches_handler_and_returns_string_id()
    {
        var (sender, repo, _) = Build();
        var dto = await sender.Send(Valid());
        Assert.Equal(1, repo.AddCalls);
        Assert.True(ObjectId.TryParse(dto.Id, out _));
        Assert.Equal(5, dto.Rating);
        Assert.Equal("viewer", dto.Type);
    }

    [Fact]
    public async Task Query_with_invalid_ObjectId_is_rejected()
    {
        var (sender, _, _) = Build();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new GetReviewByIdQuery("not-an-id")));
        Assert.Contains(ex.Errors, e => e.PropertyName == "Id");
    }

    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var (_, _, sp) = Build();
        sp.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }
}
