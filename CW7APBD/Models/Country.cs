namespace CW7APBD.Models;
using System.ComponentModel.DataAnnotations;

public class Country
{
    [Range(0, int.MaxValue)]
    public required int IdCountry { get; set; }
    [MaxLength(120)]
    public required string Name { get; set; }
}