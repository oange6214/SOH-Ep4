using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SohatNotebook.Authentication.Configuration;
using SohatNotebook.Authentication.Models.DTO.Generic;
using SohatNotebook.Authentication.Models.DTO.Incoming;
using SohatNotebook.Authentication.Models.DTO.Outgoing;
using SohatNotebook.DataService.IConfiguration;
using SohatNotebook.Entities.DbSet;

namespace SohatNotebook.Api.Controllers.v1;

public class AccountsController : BaseController
{
    // Class provided by AspNetCore Identity framework
    private readonly UserManager<IdentityUser> _userManager;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly JwtConfig _jwtConfig;

    public AccountsController(
        IUnitOfWork unitOfWork, 
        UserManager<IdentityUser> userManager,
        IOptionsMonitor<JwtConfig> optionsMonitor,
        TokenValidationParameters tokenValidationParameters) : base(unitOfWork)
    {
        _userManager = userManager;
        _jwtConfig = optionsMonitor.CurrentValue;
        _tokenValidationParameters = tokenValidationParameters;
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
            var jwtToken = await GenerateJwtToken(newUser);

            // return back to the user
            return Ok(new UserRegistrationResponseDto()
            {
                Success = true,
                Token = jwtToken.Token,
                RefreshToken = jwtToken.RefreshToken
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
                var jwtToken = await GenerateJwtToken(user);

                return Ok(new UserLoginResponseDto()
                {
                    Success = true,
                    Token = jwtToken.Token,
                    RefreshToken = jwtToken.RefreshToken
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

    [HttpPost]
    [Route("RefreshToken")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRequestDto tokenRequestDto)
    {
        if (ModelState.IsValid)
        {
            // Check if the token is valid
            var result = await VerifyToken(tokenRequestDto);

            if (result == null)
            {
                return BadRequest(new UserRegistrationResponseDto
                {
                    Success = false,
                    Errors = new List<string>(){
                        "token validation failed"
                    }
                });
            }

            return Ok(result);
        }
        else // Invalid Object
        {
            return BadRequest(new UserRegistrationResponseDto
            {
                Success = false,
                Errors = new List<string>(){
                    "Invalid payload"
                }
            });
        }
    }

    private async Task<AuthResult?> VerifyToken(TokenRequestDto tokenRequestDto)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            // We need to check the validity of the token
            var principal = tokenHandler.ValidateToken(tokenRequestDto.Token, _tokenValidationParameters, out var validatedToken);

            // We need to validate the results that has been generated for us
            // Validate if the string is an acutal JWT token not a random string
            if (validatedToken is JwtSecurityToken jwtSecurityToken)
            {
                // Check if the jwt token is created with the same algorithm as our jwt token.
                var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
                
                if (!result)
                    return null;
            }

            // We need to check the expiry date of the token
            var utcExpiryDate = long.Parse(principal.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)!.Value);

            // Convert to date to check
            var expDate = UnixTimeStampToDateTime(utcExpiryDate);

            // Checking if the jwt token has expired
            if (expDate > DateTime.UtcNow)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Jwt token has not expired"
                    }
                };
            }

            // Check if the refresh token exist
            var refreshToken = await _unitOfWork.RefreshTokens.GetByRefreshToken(tokenRequestDto.RefreshToken);

            if (refreshToken == null)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Invalid refresh token"
                    }
                };
            }

            // Check the expiry date of a refresh token
            if (refreshToken.ExpiryDate < DateTime.UtcNow)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Refresh token has expired, please login again"
                    }
                };
            }

            // Check if refresh token has been used or not
            if (refreshToken.IsUsed)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Refresh token has been used, it cannot be reused"
                    }
                };
            }

            // Check if refresh token has been revoked
            if (refreshToken.IsRevoked)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Refresh token has been revoked, it cannot be used"
                    }
                };
            }

            var jti = principal.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)!.Value;

            if (refreshToken.JwtId != jti)
            {
                return new AuthResult()
                {
                    Success = false,
                    Errors = new List<string>()
                    {
                        "Refresh token reference does not match the jwt token"
                    }
                };
            }

            // Start process and get a new token
            refreshToken.IsUsed = true;

            var updateResult = await _unitOfWork.RefreshTokens.MarkRefreshTokenAsUsed(refreshToken);
            
            if(updateResult)
            {
                await _unitOfWork.CompleteAsync();

                // Get the user to generate a new jwt token
                var dbUser = await _userManager.FindByIdAsync(refreshToken.UserId);

                if (dbUser == null)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Errors = new List<string>()
                        {
                            "Error processing request"
                        }
                    };
                }

                // Generate a jwt token
                var tokens = await GenerateJwtToken(dbUser);

                return new AuthResult
                {
                    Success = true,
                    Token = tokens.Token,
                    RefreshToken = tokens.RefreshToken
                };
            }

            return new AuthResult()
            {
                Success = false,
                Errors = new List<string>()
                {
                    "Error processing request"
                }
            };
        }
        catch (Exception)
        {
            // TODO: Add better error handling, and add a logger

            return null;
        }
    }

    private DateTime UnixTimeStampToDateTime(long utcExpiryDate)
    {
        // Sets the time to 1, Jan, 1970
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // Add the number of seconds from 1 Jan 1970
        dateTime = dateTime.AddSeconds(utcExpiryDate).ToUniversalTime();

        return dateTime;
    }

    private async Task<TokenData> GenerateJwtToken(IdentityUser user)
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
            Expires = DateTime.UtcNow.Add(_jwtConfig.ExpiryTimeFrame), // Todo update the expiration time to minutes
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature // Todo review the algorithm
            )
        };

        // Generate the security obj token
        var token = jwtHandler.CreateToken(tokenDescriptor);

        // Convert the security obj token into a string.
        var jwtToken = jwtHandler.WriteToken(token);

        // Generate a refresh token
        var refreshToekn = new RefreshToken
        {
            AddedDate = DateTime.UtcNow,
            Token = $"{RandomStringGenerator(25)}_{Guid.NewGuid()}",
            UserId = user.Id,
            IsRevoked = false,
            IsUsed = false,
            Status = 1,
            JwtId = token.Id,
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
        };

        await _unitOfWork.RefreshTokens.Add(refreshToekn);
        await _unitOfWork.CompleteAsync();

        var tokenData = new TokenData
        {
            Token = jwtToken,
            RefreshToken = refreshToekn.Token
        };

        return tokenData;
    }

    private string RandomStringGenerator(int length)
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return new string(Enumerable.Repeat(chars, length)
                                    .Select(s => s[random.Next(s.Length)])
                                    .ToArray());
    }
}