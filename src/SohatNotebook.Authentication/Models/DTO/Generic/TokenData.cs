using System.ComponentModel.DataAnnotations;

namespace SohatNotebook.Authentication.Models.DTO.Generic;

public class TokenData
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}