using  finalyearproject.Services.Interfaces;
using finalyearproject.DTOs;
using Microsoft.EntityFrameworkCore;
using finalyearproject.Data;
using finalyearproject.Wrappers;
using BCrypt.Net;



namespace finalyearproject.Services.Implementation;

public class UserServices:IUsers
{
    private ILogger<UserServices> _logger;
    private readonly AppDbContext _context;

    public UserServices(ILogger<UserServices> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }
    
    public async Task<ApiResponse<ReadUserDTOs>> CreateUserAsync(CreateUserDTO dto)
    {
        var  existinguser =  await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if(existinguser != null){
            _logger.LogError("Attempt to create user with existing email: {Email}", dto.Email);
            return ApiResponse<ReadUserDTOs>.FailureResponse("Email already in use",400);
        }
            
      var  email = dto.Email.Trim().ToLowerInvariant();
        var user = new Models.User
        {
            UserId = Guid.NewGuid(),
            FullName = dto.FullName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created new user with email: {Email}", user.Email);
        
        var response = new ReadUserDTOs
        {
            Id = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        };
        
        return ApiResponse<ReadUserDTOs>.SuccessResponse(response,"user created successfully",200); 
    }
    
    public  async Task<ApiResponse<ReadUserDTOs>> GetByIdAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) {
            _logger.LogWarning("User with id {Id} not found", id);
            return ApiResponse<ReadUserDTOs>.FailureResponse("User not found",404);
        }
        
        var response = new ReadUserDTOs
        {
            Id = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        };
        
        return ApiResponse<ReadUserDTOs>.SuccessResponse(response,"user retrieved successfully",200);
    }
    
    public async Task<ApiResponse<ReadUserDTOs>> GetByEmailAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            _logger.LogWarning("User with email {Email} not found", email);
            return ApiResponse<ReadUserDTOs>.FailureResponse("User not found",404);
        }
        var response= new ReadUserDTOs
        {
            Id = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        };
        return  ApiResponse<ReadUserDTOs>.SuccessResponse(response,"user retrieved successfully",200);  
    }
    
    public async Task<ApiResponse<IEnumerable<ReadUserDTOs>>> GetAllAsync()
    {
        var users = await _context.Users.ToListAsync();
        
        var  response = users.Select(user => new ReadUserDTOs
        {
            Id = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        });
        
        return ApiResponse<IEnumerable<ReadUserDTOs>>.SuccessResponse(response,"users retrieved successfully",200);
    }
    
    public async Task<ApiResponse<ReadUserDTOs>> UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) {
            _logger.LogWarning("User with id {Id} not found for update", id);
            return ApiResponse<ReadUserDTOs>.FailureResponse("User not found",404);
        }
        
        if (!string.IsNullOrEmpty(dto.FullName))
            user.FullName = dto.FullName;
        
        if (!string.IsNullOrEmpty(dto.Email))
            user.Email = dto.Email;
        
        await _context.SaveChangesAsync();
        
        var reaponse =new ReadUserDTOs
        {
            Id = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        };
        
        return ApiResponse<ReadUserDTOs>.SuccessResponse(reaponse,"user updated successfully",200); 
    }



    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id)
    {
        var  user  = await  _context.Users.FindAsync(id);
        _logger.LogInformation("Attempt to delete user with id: {Id}", id);
        if (user == null) {
            _logger.LogWarning("User with id {Id} not found for deletion", id);
            return ApiResponse<bool>.FailureResponse("User not found",404);
        }
        
        _logger.LogInformation("Soft deleting user with id: {UserId}", id);
        user.IsDeleted = true;
        await _context.SaveChangesAsync();
        return ApiResponse<bool>.SuccessResponse(true,"user deleted successfully",200);
    }
    
}