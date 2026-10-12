using FluentValidation;

namespace MiniErp.Application.Common;

internal static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> ValidStateCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(IndianStates.IsValidCode).WithMessage("'{PropertyValue}' is not a valid GST state code.");

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
}
