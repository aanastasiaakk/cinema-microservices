using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bookings.Bll.Dto;

namespace Bookings.Tests;

/// <summary>
/// Валідація DTO не повинна залежати від культури машини: в uk-UA десятковий роздільник — кома,
/// і [Range(typeof(decimal), "0.01", ...)] без InvariantCulture кидав виняток (500 замість 400).
/// </summary>
public class DtoValidationTests
{
    [Theory]
    [InlineData(450.00, true)]
    [InlineData(0, false)]
    [InlineData(100001, false)]
    public void PayBookingDto_amount_range_works_in_uk_UA_culture(double amount, bool expectedValid)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("uk-UA");
            var dto = new PayBookingDto { Amount = (decimal)amount, Method = "Card" };

            var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), [], validateAllProperties: true);

            Assert.Equal(expectedValid, isValid);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
