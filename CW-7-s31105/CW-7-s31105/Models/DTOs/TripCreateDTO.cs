namespace CW_7_s31105.Models.DTOs;

public class TripCreateDTO
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required DateTime DateFrom { get; set; }
    public required DateTime DateTo { get; set; }
    public required int MaxPeople { get; set; }
}