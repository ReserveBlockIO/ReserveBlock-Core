using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReserveBlockCore.Controllers;

namespace ReserveBlockCore.BTC
{
    [ActionFilterController]
    [Route("api/[controller]")]
    [Route("api/[controller]/{somePassword?}")]
    [ApiController]
    public class BTCV1Controller : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok();    
        }
    }
}
