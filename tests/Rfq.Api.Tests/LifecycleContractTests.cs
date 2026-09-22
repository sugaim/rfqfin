using Microsoft.AspNetCore.Mvc;
using Rfq.Api.RfqLifecycle;
using Rfq.Application;
using Rfq.Domain;
using Xunit;

namespace Rfq.Api.Tests;

public sealed class LifecycleContractTests
{
    [Theory]
    [InlineData(nameof(RfqLifecycleController.CloseHit), "{caseId:long}/close/hit")]
    [InlineData(nameof(RfqLifecycleController.CloseAway), "{caseId:long}/close/away")]
    [InlineData(
        nameof(RfqLifecycleController.CorrectToHit),
        "{caseId:long}/outcome/correct-to-hit")]
    [InlineData(
        nameof(RfqLifecycleController.CorrectToAway),
        "{caseId:long}/outcome/correct-to-away")]
    public void Lifecycle_commands_have_explicit_routes_without_outcome_selector(
        string methodName, string route)
    {
        System.Reflection.MethodInfo method = typeof(RfqLifecycleController).GetMethod(methodName)!;
        Assert.Equal(
            route,
            method.GetCustomAttributes(typeof(HttpPostAttribute), false)
                .Cast<HttpPostAttribute>().Single().Template);
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter =>
                parameter.ParameterType == typeof(RfqStatus));
    }

    [Fact]
    public void Application_close_and_correction_commands_do_not_accept_rfq_status()
    {
        foreach (Type? type in new[]
        {
            typeof(CloseHitRfq),
            typeof(CloseAwayRfq),
            typeof(CorrectOutcomeToHit),
            typeof(CorrectOutcomeToAway),
        })
        {
            System.Reflection.MethodInfo method = Assert.Single(type.GetMethods(), value => value.Name == "ExecuteAsync");
            Assert.DoesNotContain(
                method.GetParameters(),
                parameter =>
                    parameter.ParameterType == typeof(RfqStatus));
        }
    }
}
