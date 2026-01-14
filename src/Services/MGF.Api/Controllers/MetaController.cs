namespace MGF.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MGF.Api.Services;

[ApiController]
[Route("api")]
public sealed class MetaController : ControllerBase
{
    private readonly MetaService metaService;

    public MetaController(MetaService metaService)
    {
        this.metaService = metaService;
    }

    [HttpGet("meta")]
    public ActionResult<MetaService.MetaDto> GetMeta()
    {
        return Ok(metaService.GetMeta());
    }
}
