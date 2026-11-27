using System.Xml.Linq;

namespace Platform.Tests;

/// <summary>Посилання між проєктами платформи (*.csproj): AppHost → усі; решта → лише ServiceDefaults.</summary>
public class SolutionStructureTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Platform.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string[] Refs(string csproj) => XDocument.Load(Path.Combine(Root, csproj))
        .Descendants("ProjectReference")
        .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
        .Order().ToArray();

    [Fact]
    public void AppHost_references_all_runnable_projects()
        => Assert.Equal(
            ["Bookings.Api", "Catalog.Api", "Platform.Aggregator", "Platform.ApiGateway", "Reviews.WebApi"],
            Refs("Platform.AppHost/Platform.AppHost.csproj"));

    [Theory]
    [InlineData("Platform.ApiGateway/Platform.ApiGateway.csproj")]
    [InlineData("Platform.Aggregator/Platform.Aggregator.csproj")]
    public void Gateway_and_Aggregator_reference_only_ServiceDefaults(string csproj)
        => Assert.Equal(["Platform.ServiceDefaults"], Refs(csproj));

    [Theory]
    [InlineData("Services/Bookings/Bookings.Api/Bookings.Api.csproj")]
    [InlineData("Services/Catalog/Catalog.Api/Catalog.Api.csproj")]
    [InlineData("Services/Reviews/Reviews.WebApi/Reviews.WebApi.csproj")]
    public void Services_reference_ServiceDefaults_and_never_AppHost(string csproj)
    {
        var refs = Refs(csproj);
        Assert.Contains("Platform.ServiceDefaults", refs);
        Assert.DoesNotContain(refs, r => r.StartsWith("Platform.") && r != "Platform.ServiceDefaults");
    }

    [Fact]
    public void Nobody_references_AppHost_and_ServiceDefaults_references_nobody()
    {
        Assert.Empty(Refs("Platform.ServiceDefaults/Platform.ServiceDefaults.csproj"));
        var all = Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        foreach (var csproj in all)
            Assert.DoesNotContain("Platform.AppHost", Refs(Path.GetRelativePath(Root, csproj)));
    }
}
