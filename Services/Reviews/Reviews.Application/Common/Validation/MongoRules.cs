using FluentValidation;
using MongoDB.Bson;

namespace Reviews.Application.Common.Validation;

/// <summary>MongoDB-специфічні правила валідації.</summary>
public static class MongoRules
{
    public static IRuleBuilderOptions<T, string> MustBeObjectId<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().Must(id => ObjectId.TryParse(id, out _))
               .WithMessage("'{PropertyName}' must be a valid 24-character hex ObjectId");

    public static bool IsObjectId(string? id) => ObjectId.TryParse(id, out _);
}
