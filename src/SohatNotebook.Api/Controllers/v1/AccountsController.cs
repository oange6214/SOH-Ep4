using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SohatNotebook.Authentication.Configuration;
using SohatNotebook.Authentication.Models.DTO.Incoming;
using SohatNotebook.Authentication.Models.DTO.Outgoing;
using SohatNotebook.DataService.IConfiguration;
using SohatNotebook.Entities.DbSet;

namespace SohatNotebook.Api.Controllers.v1;

public class AccountsController : BaseController
{
    // Class provided by AspNetCore Identity framework
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtConfig _jwtConfig;

    public AccountsController(
        IUnitOfWork unitOfWork, 
        UserManager<IdentityUser> userManager,
        IOptionsMonitor<JwtConfig> optionsMonitor) : base(unitOfWork)
    {
        _userManager = userManager;
        _jwtConfig = optionsMonitor.CurrentValue;
    }

    // Register Action
    [HttpPost]
    [Route("Register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDto registrationDto)
    {
        // Check the model or obj we are recieving is valid
        if (ModelState.IsValid)
        {
            // Check if the emial already exist
            var userExist = await _userManager.FindByEmailAsync(registrationDto.Email);

            if (userExist is not null) // Email is already in the table
            {
                return BadRequest(new UserRegistrationResponseDto{
                    Success = false,
                    Errors = new List<string>(){
                        "Email already in use"
                    }
                });
            }

            // Add the user
            var newUser = new IdentityUser()
            {
                Email = registrationDto.Email,
                UserName = registrationDto.Email,
                EmailConfirmed = true // todo build email functionality to send to the user to confirm email
            };

            // Adding the user to the table
            var isCreated = await _userManager.CreateAsync(newUser, registrationDto.Password);

            if (!isCreated.Succeeded)   // When the registration has failed
            {
                return BadRequest(new UserRegistrationResponseDto()
                {
                    Success = isCreated.Succeeded,
                    Errors = isCreated.Errors.Select(x => x.Description).ToList()
                });
            }

            // Adding user to the database
            var user = new User()
            {
                IdentityId = new Guid(newUser.Id),
                LastName = registrationDto.LastName,
                FirstName = registrationDto.FirstName,
                Email = registrationDto.Email,
                DateOfBirth = DateTime.UtcNow,
                Phone = "",
                Country = "",
                Status = 1
            };
            
            await _unitOfWork.Users.Add(user);
            await _unitOfWork.CompleteAsync();

            // Create a jwt token
            var token = GenerateJwtToken(newUser);

            // return back to the user
            return Ok(new UserRegistrationResponseDto()
            {
                Success = true,
                Token = token
            });
        }
        else // Invalid Object
        {
            return BadRequest(new UserRegistrationResponseDto{
                Success = false,
                Errors = new List<string>(){
                    "Invalid payload"
                }
            });
        }
    }

    [HttpPost]
    [Route("Login")]
    public async Task<IActionResult> Login([FromBody] UserLoginRequestDto loginDto)
    {
        if (ModelState.IsValid)
        {
            // 1 - Check if email exist
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user is null)
            {
                return BadRequest(new UserLoginResponseDto(){
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Invalid authentication request"
                    }
                });
            }

            // 2 - Check if the user has a valid password
            var isCorrect = await _userManager.CheckPasswordAsync(user, loginDto.Password);

            if (isCorrect)
            {
                // We need to generate a Jwt Token
                var jwtToken = GenerateJwtToken(user);

                return Ok(new UserLoginResponseDto()
                {
                    Success = true,
                    Token = jwtToken
                });
            }
            else
            {
                // Password doesn't match
                return BadRequest(new UserLoginResponseDto(){
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Invalid authentication request"
                    }
                });
            }

        }
        else // Invalid Object
        {
            return BadRequest(new UserRegistrationResponseDto{
                Success = false,
                Errors = new List<string>(){
                    "Invalid payload"
                }
            });
        }
    }

    private string GenerateJwtToken(IdentityUser user)
    {
        // The handler is going to be responsible for creating the token
        var jwtHandler = new JwtSecurityTokenHandler();

        // Get the security key
        var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new []
            {
                new Claim("Id", user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email!),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Used by the refresh token
            }),
            Expires = DateTime.UtcNow.AddHours(3), // Todo update the expiration time to minutes
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature // Todo review the algorithm
            )
        };

        // Generate the security obj token
        var token = jwtHandler.CreateToken(tokenDescriptor);

        // Convert the security obj token into a string.
        var jwtToken = jwtHandler.WriteToken(token);

        return jwtToken;
    }
}