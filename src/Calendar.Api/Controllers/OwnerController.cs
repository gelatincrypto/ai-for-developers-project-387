using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Calendar.Api.Generated.Controllers;
using Calendar.Api.Generated.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Calendar.Api.Controllers;

[Authorize]
public sealed class OwnerController(IConfiguration configuration) : OwnerApiController
{
    public override IActionResult GetOwnerSession() => Ok(new OwnerSession { Username = User.Identity!.Name! });

    [AllowAnonymous]
    public override IActionResult Login(OwnerCredentials ownerCredentials)
    {
        var username = configuration["Owner:Username"];
        var password = configuration["Owner:Password"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
            !Matches(ownerCredentials.Username, username) || !Matches(ownerCredentials.Password, password))
        {
            return Unauthorized();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, username)], CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity))
            .GetAwaiter().GetResult();
        return Ok(new OwnerSession { Username = username });
    }

    public override IActionResult Logout()
    {
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
        return NoContent();
    }

    private static bool Matches(string submitted, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(submitted)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
