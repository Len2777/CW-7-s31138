namespace CW7APBD.Models;

using System.ComponentModel.DataAnnotations;

public class Client
{
    [Range(0, int.MaxValue)]
    public required int IdClient { get; set; }
    [MaxLength(120)]
    public required string FirstName { get; set; }
    [MaxLength(120)]
    public required string LastName { get; set; }
    [MaxLength(120), EmailAddress]
    public required string Email { get; set; }
    [MaxLength(120), Phone]
    public required string Telephone { get; set; }
    [MaxLength(120)]
    public required string Pesel { get; set; }
}