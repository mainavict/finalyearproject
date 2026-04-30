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


    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _userService.GetAllAsync();
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
        
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await _userService.GetByIdAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
        
    }
    
    [HttpPut]
    
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _userService.UpdateUserAsync(id, dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{userid}/Deleteuser")]

    public async Task<IActionResult> DeleteUser(Guid userid)
    {
        var result = await _userService.DeleteUserAsync(userid);
        if (!result.Success)
        {
            return BadRequest(result);


        }

        return Ok(result);

    }


}