using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Calendar.Api.Tests;

public class OwnerTests
{
    [Fact]
    public async Task OwnerSession_RequiresLogin_AndLogoutRevokesAccess()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Owner:Username", "test-owner")
                .UseSetting("Owner:Password", "test-password"));
        using var client = factory.CreateClient();
        using var guest = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.DeleteAsync("/api/owner/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/owner/session", new { username = "test-owner", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner/session")).StatusCode);

        using var login = await client.PostAsJsonAsync("/api/owner/session",
            new { username = "test-owner", password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("test-owner", (await login.Content.ReadFromJsonAsync<Session>())?.Username);
        Assert.NotNull(login.Headers.GetValues("Set-Cookie").SingleOrDefault());

        using var session = await client.GetAsync("/api/owner/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal("test-owner", (await session.Content.ReadFromJsonAsync<Session>())?.Username);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/owner/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/owner/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task NoConfiguredCredentials_FailsClosed()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/owner/session",
            new { username = "owner", password = "password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record Session(string Username);
}
