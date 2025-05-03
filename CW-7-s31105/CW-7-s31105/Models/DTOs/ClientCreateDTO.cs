namespace CW_7_s31105.Models.DTOs;

public class ClientCreateDTO
{
    public required string FirstName { get; set; }
    public required string Lastname { get; set; }
    public required string Email { get; set; }
    public required string Telephone { get; set; }
    public required string Pesel { get; set; }
}