using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace SohatNotebook.Entities.DbSet;
public class RefreshToken : BaseEntity
{
    /// <summary>
    /// User Id when logged in
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    /// <summary>
    /// The id generated when a jwt id has been requested
    /// </summary>
    public string JwtId { get; set; } = string.Empty;
    /// <summary>
    /// To make sure that the token is only used once
    /// </summary>
    public bool IsUsed { get; set; }
    /// <summary>
    /// To make sure they are valid
    /// </summary>
    public bool IsRevoked { get; set; }

    public DateTime ExpiryDate { get; set; }

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }
}