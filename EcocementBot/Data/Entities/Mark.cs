using System.ComponentModel.DataAnnotations;

namespace EcocementBot.Data.Entities;

public class Mark
{
    [Key]
    public string Name { get; set; }
}
