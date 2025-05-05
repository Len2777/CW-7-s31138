namespace CW7APBD.Models.Dtos;

using System.ComponentModel.DataAnnotations;

public class TripGetDTO
{
    [Range(0, int.MaxValue)]
    public required int TripId  { get; set; }
    [MaxLength(120)]
    public required string Name { get; set; }
    [MaxLength(220)]
    public required string Description { get; set; }
    public required DateTime DateFrom { get; set; }
    public required DateTime DateTo { get; set; }
    [Range(0, int.MaxValue)]
    public required int MaxPeople { get; set; }
    public required List<Country> Countries { get; set; }
}