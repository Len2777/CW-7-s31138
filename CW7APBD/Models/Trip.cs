namespace CW7APBD.Models;

using System.ComponentModel.DataAnnotations;


public class Trip
{
    [Range(0, int.MaxValue)]
    public required int IdTrip { get; set; }
    [MaxLength(120)]
    public required string Name { get; set; }
    [MaxLength(220)]
    public required string Description { get; set; }
    public required DateTime DateFrom { get; set; }
    public required DateTime DateTo { get; set; }
    [Range(0, int.MaxValue)]
    public required int MaxPeople { get; set; }
}