namespace MGF.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MGF.Api.Services;

[ApiController]
[Route("api/people")]
public sealed class PeopleController : ControllerBase
{
    private readonly PeopleService people;

    public PeopleController(PeopleService people)
    {
        this.people = people;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PeopleService.PersonDto>>> GetPeople(
        [FromQuery] string? role = null,
        [FromQuery] bool active = false,
        CancellationToken cancellationToken = default
    )
    {
        var results = await people.GetPeopleAsync(role, active, cancellationToken);
        return Ok(results);
    }
}

