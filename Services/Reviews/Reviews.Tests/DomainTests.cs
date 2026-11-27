using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Tests;

/// <summary>Правила домену перевіряються без жодного звернення до БД.</summary>
public class DomainTests
{
    private static readonly Author Olena = new(1, "Олена Коваль");

    [Fact]
    public void Rating_10_throws_DomainException()
        => Assert.Throws<DomainException>(() =>
            new Review(ReviewType.Viewer, 1, "Довбуш", Olena, new Rating(10), "Текст"));

    [Fact]
    public void Value_objects_are_compared_by_value()
    {
        Assert.Equal(new Author(1, "Олена Коваль"), new Author(1, "Олена Коваль"));
        Assert.NotEqual(new Author(1, "Олена Коваль"), new Author(2, "Олена Коваль"));
        Assert.True(new Rating(4) == new Rating(4));
        Assert.Equal(new CriticInfo("Кіноблог", 90).GetHashCode(), new CriticInfo("Кіноблог", 90).GetHashCode());
    }

    [Theory]
    [InlineData(0, "Ім'я")]
    [InlineData(1, " ")]
    public void Author_is_validated_in_constructor(long id, string name)
        => Assert.Throws<DomainException>(() => new Author(id, name));

    [Fact]
    public void Review_type_rules()
    {
        Assert.Throws<DomainException>(() => new Review(ReviewType.Viewer, 1, "Довбуш", Olena, new Rating(5), null));
        Assert.Throws<DomainException>(() => new Review(ReviewType.Critic, 1, "Довбуш", Olena, new Rating(5), "Текст"));
        Assert.Throws<DomainException>(() =>
            new Review(ReviewType.Viewer, 1, "Довбуш", Olena, new Rating(5), "Текст", new CriticInfo("X", 1)));
        var quick = new Review(ReviewType.Quick, 1, "Довбуш", Olena, new Rating(4), null);
        Assert.Null(quick.Text);
    }

    [Fact]
    public void Comments_are_encapsulated_and_deleted_review_is_read_only()
    {
        var review = new Review(ReviewType.Viewer, 1, "Довбуш", Olena, new Rating(5), "Супер");
        review.AddComment("Андрій", "Згоден", DateTime.UtcNow);
        Assert.Single(review.Comments);
        Assert.Throws<DomainException>(() => review.AddComment("Андрій", " ", DateTime.UtcNow));

        review.Delete();
        Assert.True(review.IsDeleted);
        Assert.Throws<DomainException>(() => review.Edit(new Rating(3), "Інше", null));
    }

    [Fact]
    public void Discussion_keeps_only_three_recent_messages()
    {
        var d = new Discussion(1, "Довбуш", "Історичність", Olena, ["Історія", "історія"]);
        for (var i = 1; i <= 5; i++) d.PostMessage(Olena, $"Повідомлення {i}", DateTime.UtcNow.AddMinutes(i));

        Assert.Equal(5, d.MessagesCount);
        Assert.Equal(3, d.RecentMessages.Count);
        Assert.Equal("Повідомлення 5", d.RecentMessages.Last().Text);
        Assert.Single(d.Tags);

        d.Close();
        Assert.Throws<DomainException>(() => d.PostMessage(Olena, "Ще", DateTime.UtcNow));
    }
}
