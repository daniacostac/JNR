// Models/User.cs

using JNR.Models;

public partial class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? ProfilePicturePath { get; set; }

    public virtual ICollection<Useralbumrating> Useralbumratings { get; set; } = new List<Useralbumrating>();
}