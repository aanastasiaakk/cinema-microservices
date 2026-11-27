using System.Xml.Linq;

namespace Reviews.Tests;

/// <summary>Правило залежностей Clean Architecture перевіряється за самими *.csproj.</summary>
public class ArchitectureTests
{
    private static string Csproj(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Platform.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "Services", "Reviews", project, $"{project}.csproj");
    }

    private static (string[] Projects, string[] Packages) Refs(string project)
    {
        var xml = XDocument.Load(Csproj(project));
        return (xml.Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/'))).ToArray(),
                xml.Descendants("PackageReference").Select(e => e.Attribute("Include")!.Value).ToArray());
    }

    [Fact]
    public void Domain_has_no_project_references_and_no_mongo_driver()
    {
        var (projects, packages) = Refs("Reviews.Domain");
        Assert.Empty(projects);
        Assert.Equal(["MongoDB.Bson"], packages);
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        var (projects, packages) = Refs("Reviews.Application");
        Assert.Equal(["Reviews.Domain"], projects);
        Assert.DoesNotContain("MongoDB.Driver", packages);
    }

    [Fact]
    public void Infrastructure_and_WebApi_point_inwards()
    {
        Assert.Equal(["Reviews.Application", "Reviews.Domain"], Refs("Reviews.Infrastructure").Projects.Order());
        Assert.Equal(["Reviews.Application", "Reviews.Infrastructure"], Refs("Reviews.WebApi").Projects.Order());
    }
}
