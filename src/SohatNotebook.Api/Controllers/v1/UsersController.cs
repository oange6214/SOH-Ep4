using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SohatNotebook.DataService.IConfiguration;
using SohatNotebook.Entities.DbSet;
using SohatNotebook.Entities.Dtos.Incoming;

namespace SohatNotebook.Api.Controllers.v1;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UsersController : BaseController
{
    public UsersController(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    // Get all
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _unitOfWork.Users.All();
        return Ok(users);
    }

    // Post
    [HttpPost]
    public async Task<IActionResult> AddUser(UserDto userDto)
    {
        var user = new User()
        {
            LastName = userDto.LastName,
            FirstName = userDto.FirstName,
            Email = userDto.Email,
            DateOfBirth = Convert.ToDateTime(userDto.DateOfBirth),
            Phone = userDto.Phone,
            Country = userDto.Country,
            Status = 1
        };
        
        await _unitOfWork.Users.Add(user);
        await _unitOfWork.CompleteAsync();

        return CreatedAtRoute("GetUser", new { id = user.Id }, user); // return a 201
    }

    // Get
    [HttpGet]
    [Route("GetUser", Name = "GetUser")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _unitOfWork.Users.GetById(id);

        return Ok(user);
    }

}