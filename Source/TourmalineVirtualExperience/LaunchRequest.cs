using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

public class LaunchRequest
{
    /// <summary>
    /// Nombre de la carpeta de la ruta
    /// </summary>
    [Required]
    [DefaultValue("SFM")]
    public string Route { get; set; } = "SFM";

    /// <summary>
    /// Nombre de la actividad o subcarpeta dentro de la ruta
    /// </summary>
    [Required]
    [DefaultValue("T21")]
    public string RoutePath { get; set; } = "T21";

    /// <summary>
    /// Nombre del consist o locomotora
    /// </summary>
    [Required]
    [DefaultValue("440")]
    public string Consist { get; set; } = "440";

    /// <summary>
    /// Hora de inicio de la simulación
    /// </summary>
    [DefaultValue("12:00")]
    public string Now { get; set; } = "12:00";

    /// <summary>
    /// Estación del año
    /// </summary>
    [DefaultValue(0)]
    public int Season { get; set; } = 0;

    /// <summary>
    /// Meteorología
    /// </summary>
    [DefaultValue(0)]
    public int Climate { get; set; } = 0;

}
