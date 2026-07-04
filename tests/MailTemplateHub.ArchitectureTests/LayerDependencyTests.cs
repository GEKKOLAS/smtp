using MailTemplateHub.Domain.Errors;
using NetArchTest.Rules;

namespace MailTemplateHub.ArchitectureTests;

/// <summary>Enforces the dependency rules from docs/spec/03-architecture.md §2.</summary>
public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly Domain =
        typeof(DomainException).Assembly;

    private static readonly System.Reflection.Assembly Application =
        typeof(MailTemplateHub.Application.DependencyInjection).Assembly;

    private static readonly System.Reflection.Assembly Infrastructure =
        typeof(MailTemplateHub.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny(
                "MailTemplateHub.Application",
                "MailTemplateHub.Infrastructure",
                "MailTemplateHub.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Offenders(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny(
                "MailTemplateHub.Infrastructure",
                "MailTemplateHub.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Offenders(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot().HaveDependencyOn("MailTemplateHub.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Offenders(result));
    }

    private static string Offenders(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
