using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistemas do traje (EVA): bateria, oxigênio, dose de radiação acumulada,
/// lanterna e alertas. O consumo usa o tempo SIMULADO (MarsEnvironment.timeScale).
/// </summary>
public class SuitSystems : MonoBehaviour
{
    [Header("Bateria (%)")]
    public float battery = 100f;
    public float drainPerHour = 5f;
    public float sprintExtraPerHour = 7f;
    public float flashlightPerHour = 1.5f;
    public float stormExtraPerHour = 2f;     // aquecimento extra durante tempestade
    public float rechargePerRealSecond = 12f;

    [Header("Oxigênio (horas)")]
    public float oxygenHours = 8f;
    public float maxOxygenHours = 8f;
    public float refillPerRealSecond = 1f;

    [Header("Radiação")]
    public float doseMilliSv = 0f;

    [Header("Lanterna")]
    public Light flashlight;
    public bool flashlightOn;

    public PointOfInterest CurrentStation { get; private set; }
    public PointOfInterest CurrentHazard { get; private set; }
    public float EvaHours { get; private set; }
    public readonly List<string> Alerts = new List<string>();

    AstronautController ctrl;
    MarsEnvironment env;

    void Start()
    {
        ctrl = GetComponent<AstronautController>();
        env = MarsEnvironment.Instance;
    }

    public float CurrentDrainPerHour
    {
        get
        {
            float d = drainPerHour;
            if (ctrl != null && ctrl.IsSprinting) d += sprintExtraPerHour;
            if (flashlightOn) d += flashlightPerHour;
            if (env != null && env.dustStorm) d += stormExtraPerHour;
            return d;
        }
    }

    /// <summary>Horas simuladas por segundo real.</summary>
    public float SimHoursPerRealSecond { get { return (env != null ? env.timeScale : 60f) / 3600f; } }

    /// <summary>Quantos metros dá para andar com a bateria atual (andando, sem correr).</summary>
    public float RangeMeters
    {
        get
        {
            float hours = battery / Mathf.Max(0.01f, drainPerHour + (flashlightOn ? flashlightPerHour : 0f));
            float realSeconds = hours / SimHoursPerRealSecond;
            return realSeconds * (ctrl != null ? ctrl.walkSpeed : 2.5f);
        }
    }

    void Update()
    {
        if (env == null) env = MarsEnvironment.Instance;
        float simH = Time.deltaTime * SimHoursPerRealSecond;
        EvaHours += simH;

        // Lanterna
        if (MarsInput.Down(MarsKey.Flashlight)) flashlightOn = !flashlightOn;
        if (battery <= 0f) flashlightOn = false;
        if (flashlight != null) flashlight.enabled = flashlightOn;

        // Consumo
        battery = Mathf.Max(0f, battery - CurrentDrainPerHour * simH);
        float o2Rate = (ctrl != null && ctrl.IsSprinting) ? 1.6f : 1f;
        oxygenHours = Mathf.Max(0f, oxygenHours - o2Rate * simH);
        if (env != null) doseMilliSv += env.DoseRateMicroSvPerHour * simH / 1000f;

        // Estações / perigos próximos
        CurrentStation = null;
        CurrentHazard = null;
        foreach (var poi in PointOfInterest.All)
        {
            float dist = Vector3.Distance(transform.position, poi.transform.position);
            if (dist > poi.radius) continue;
            if (poi.Recharges)
            {
                battery = Mathf.Min(100f, battery + rechargePerRealSecond * Time.deltaTime);
                CurrentStation = poi;
            }
            if (poi.RefillsOxygen)
            {
                oxygenHours = Mathf.Min(maxOxygenHours, oxygenHours + refillPerRealSecond * Time.deltaTime);
                CurrentStation = poi;
            }
            if (poi.type == POIType.Hazard) CurrentHazard = poi;
        }

        if (ctrl != null) ctrl.speedMultiplier = battery <= 0f ? 0.4f : 1f;

        BuildAlerts();
    }

    void BuildAlerts()
    {
        Alerts.Clear();
        if (oxygenHours <= 0f) Alerts.Add("!! O2 ESGOTADO – volte à base (R = resgate)");
        else if (oxygenHours < 1f) Alerts.Add("O2 crítico: menos de 1 h");
        else if (oxygenHours < 2f) Alerts.Add("O2 baixo: planeje o retorno");

        if (battery <= 0f) Alerts.Add("!! BATERIA ESGOTADA – mobilidade reduzida");
        else if (battery < 20f) Alerts.Add("Bateria baixa: procure uma estação de recarga");

        if (env != null)
        {
            if (env.DustOpacity > 2f) Alerts.Add("Tempestade de poeira (τ " + env.DustOpacity.ToString("0.0") + ") – visibilidade baixa");
            if (env.SunElevation < -2f) Alerts.Add("Noite marciana – use a lanterna (F)");
            if (env.AirTempC < -80f) Alerts.Add("Frio extremo: " + env.AirTempC.ToString("0") + " °C");
        }

        if (CurrentHazard != null) Alerts.Add("PERIGO: " + CurrentHazard.displayName);
        if (doseMilliSv > 1f) Alerts.Add("Dose de radiação da EVA acima de 1 mSv");
    }
}
