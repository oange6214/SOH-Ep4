namespace SohatNotebook.Authentication.Models.DTO.Outgoing;

public class AuthResult
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = null!;
}
