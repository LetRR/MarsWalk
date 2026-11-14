using System.Collections.Generic;
using UnityEngine;

public enum POIType
{
    Habitat,          // reabastece O2 e bateria
    ChargingStation,  // recarrega bateria
    WaterIce,         // recurso: gelo de água
    Minerals,         // recurso: minerais (argilas, carbonatos)
    ScienceTarget,    // alvo científico
    SafeZone,         // abrigo / zona de pouso de contingência
    Hazard            // perigo (campo de rochas, duna, declive)
}

/// <summary>
/// Ponto de interesse exibido no HUD e no mapa. Pode ser colocado à mão na cena
/// (adicione este componente a qualquer objeto) ou criado pelo MarsBootstrap.
/// </summary>
public class PointOfInterest : MonoBehaviour
{
    public static readonly List<PointOfInterest> All = new List<PointOfInterest>();

    public string displayName = "Ponto de interesse";
    public POIType type = POIType.ScienceTarget;
    [TextArea(2, 5)] public string description = "";
    [Tooltip("Missão/instrumento da NASA de onde vem o dado")]
    public string dataSource = "";
    [Tooltip("Raio de efeito (recarga, perigo) em metros")]
    public float radius = 10f;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static Color ColorFor(POIType t)
    {
        switch (t)
        {
            case POIType.Habitat: return new Color(1f, 1f, 1f);
            case POIType.ChargingStation: return new Color(1f, 0.85f, 0.1f);
            case POIType.WaterIce: return new Color(0.3f, 0.75f, 1f);
            case POIType.Minerals: return new Color(0.35f, 0.95f, 0.45f);
            case POIType.ScienceTarget: return new Color(0.85f, 0.45f, 1f);
            case POIType.SafeZone: return new Color(0.2f, 1f, 0.8f);
            default: return new Color(1f, 0.25f, 0.2f);
        }
    }

    public static string Label(POIType t)
    {
        switch (t)
        {
            case POIType.Habitat: return "Habitat";
            case POIType.ChargingStation: return "Estação de recarga";
            case POIType.WaterIce: return "Gelo de água";
            case POIType.Minerals: return "Minerais";
            case POIType.ScienceTarget: return "Alvo científico";
            case POIType.SafeZone: return "Zona segura";
            default: return "Perigo";
        }
    }

    public bool Recharges { get { return type == POIType.ChargingStation || type == POIType.Habitat; } }
    public bool RefillsOxygen { get { return type == POIType.Habitat || type == POIType.SafeZone; } }

    void OnDrawGizmos()
    {
        Gizmos.color = ColorFor(type);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
