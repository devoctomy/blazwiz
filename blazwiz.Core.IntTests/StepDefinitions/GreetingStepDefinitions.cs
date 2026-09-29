using System.Globalization;
using blazwiz.Core.Greetings;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.IntTests.StepDefinitions;

[Binding]
public sealed class GreetingStepDefinitions(TestContext testContext)
{
    [Given("the current local time is {word}")]
    public void GivenTheCurrentLocalTimeIs(string time)
    {
        var timeOfDay = TimeSpan.ParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture);
        testContext.CurrentTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Add(timeOfDay);
    }

    [Given("the user is called {word}")]
    public void GivenTheUserIsCalled(string name)
    {
        testContext.UserName = name;
    }

    [When("the user is greeted")]
    public void WhenTheUserIsGreeted()
    {
        var systemClock = new Mock<ISystemClock>();
        systemClock.SetupGet(x => x.Now).Returns(testContext.CurrentTime!.Value);

        var greetingService = new GreetingService(systemClock.Object);
        testContext.Greeting = greetingService.GetGreeting(testContext.UserName);
    }

    [Then("the greeting should be {string}")]
    public void ThenTheGreetingShouldBe(string expected)
    {
        Assert.Equal(expected, testContext.Greeting);
    }
}
