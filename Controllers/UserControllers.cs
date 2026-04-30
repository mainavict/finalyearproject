using Microsoft.AspNetCore.Mvc;
using finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;

namespace finalyearproject.Controllers;
[Route("api/[controller]")]
[ApiController]
public class UserControllers:ControllerBase
{
    private readonly IUsers _userService;
    
    public UserControllers(IUsers userService)
    {
        _userService = userService;
    }
    
    
    [HttpPost]
    
    public  async  Task <IActionResult> CreateUser([FromBody] CreateUserDTO dto)
    {
        var result = await _userService.CreateUserAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    
    
    
    
}