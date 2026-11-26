using AutoMapper;
using Bookings.Bll.Mapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookings.Tests;

public class MappingConfigurationTests
{
    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(BookingProfile).Assembly), NullLoggerFactory.Instance);

        config.AssertConfigurationIsValid();
    }
}
