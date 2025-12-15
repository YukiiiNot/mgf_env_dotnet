namespace MGF.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MGF.Api.Services;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly ClientsService clients;

    public ClientsController(ClientsService clients)
    {
        this.clients = clients;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientsService.ClientDto>>> GetClients(
        [FromQuery] bool active = false,
        CancellationToken cancellationToken = default
    )
    {
        var results = await clients.GetClientsAsync(active, cancellationToken);
        return Ok(results);
    }
}

