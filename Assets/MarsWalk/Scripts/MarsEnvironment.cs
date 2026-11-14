using System;
using UnityEngine;

/// <summary>
/// Modelo do ambiente marciano: tempo (sol / LMST), posição do Sol, coordenadas,
/// pressão, temperatura, vento, poeira (opacidade tau), irradiância e radiação.
///
/// Os valores são MODELADOS a partir de médias publicadas por missões da NASA:
///  - Tempo marciano: algoritmo Mars24 (Allison &amp; McEwen 2000, NASA GISS)
///  - Pressão / temperatura: Curiosity REMS, Perseverance MEDA, Viking Landers
///  - Radiação: Curiosity RAD (~0,67 mSv/sol na superfície)
///  - Elevação: MOLA (Mars Global Surveyor) / HiRISE DTM (MRO)
///  - Opacidade da poeira: MCS (MRO) e câmeras dos rovers
/// </summary>
public class MarsEnvironment : MonoBehaviour
{
    public static MarsEnvironment Instance { get; private set; }

    [Header("Local de referência (centro do mapa)")]
    public string siteName = "Cratera Jezero";
    public double referenceLatitude = 18.4447;   // °N
    public double referenceLongitude = 77.4508;  // °E
    public float referenceElevation = -2640f;    // m em relação ao datum MOLA

    [Header("Tempo")]
    [Tooltip("Segundos marcianos simulados por segundo real")]
    public float timeScale = 60f;
    public float startLocalHour = 8.5f;
    public bool startAtRealMarsTime = false;

    [Header("Referências (preenchidas pelo MarsBootstrap)")]
    public Light sun;
    public Camera cam;
    public Transform player;
    public Terrain terrain;

    [Header("Eventos")]
    public bool dustStorm = false;

    // ---- Constantes físicas ----
    public const double SolSeconds = 88775.244;
    public const float MarsRadius = 3389500f;      // m
    public const float Gravity = 3.721f;          // m/s²
    const float ScaleHeight = 11100f;             // m (altura de escala da atmosfera)
    const float DatumPressure = 610f;             // Pa no datum (pressão média global)

    // ---- Estado calculado (lido pelo dashboard) ----
    public double MarsSolDate { get; private set; }
    public double LocalMeanSolarTime { get; private set; }  // horas 0–24
    public int Sol { get; private set; }
    public float SolarLongitude { get; private set; }      // Ls, graus (estação)
    public float SunElevation { get; private set; }        // graus
    public float SunAzimuth { get; private set; }          // graus, 0 = Norte
    public float SunriseLMST { get; private set; }
    public float SunsetLMST { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public float Elevation { get; private set; }
    public float PressurePa { get; private set; }
    public float AirTempC { get; private set; }
    public float GroundTempC { get; private set; }
    public float WindSpeed { get; private set; }
    public float WindDirection { get; private set; }
    public float DustOpacity { get; private set; }         // tau
    public float SolarIrradiance { get; private set; }     // W/m²
    public float DoseRateMicroSvPerHour { get; private set; }
    public float HeadingDeg { get; private set; }
    public DateTime EarthUtc { get; private set; }

    float elevationOffset;
    float stormBlend;

    void Awake() { Instance = this; }

    void Start()
    {
        double msdNow = MsdFromUtc(DateTime.UtcNow);
        if (startAtRealMarsTime)
        {
            MarsSolDate = msdNow;
        }
        else
        {
            // Mesmo sol de hoje, mas começando no horário local escolhido
            double mtc = startLocalHour - referenceLongitude / 15.0;
            mtc = ((mtc % 24.0) + 24.0) % 24.0;
            MarsSolDate = Math.Floor(msdNow) + mtc / 24.0;
        }

        // Calibra a elevação: o centro do mapa (0,0) = referenceElevation
        if (terrain != null)
            elevationOffset = referenceElevation - terrain.SampleHeight(Vector3.zero) - terrain.transform.position.y;
        else
            elevationOffset = referenceElevation;

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 5000f;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    void Update()
    {
        if (MarsInput.Down(MarsKey.TimeFaster)) timeScale = Mathf.Min(timeScale * 2f, 3840f);
        if (MarsInput.Down(MarsKey.TimeSlower)) timeScale = Mathf.Max(timeScale * 0.5f, 1f);
        if (MarsInput.Down(MarsKey.DustStorm)) dustStorm = !dustStorm;

        MarsSolDate += Time.deltaTime * timeScale / SolSeconds;
        UpdatePosition();
        UpdateTime();
        UpdateSunAndSky();
        UpdateAtmosphere();
    }

    // ------------------------------------------------------------------ Tempo
    public static double MsdFromUtc(DateTime utc)
    {
        double unix = (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        double jdUT = unix / 86400.0 + 2440587.5;
        double jdTT = jdUT + 69.184 / 86400.0;
        return (jdTT - 2405522.0028779) / 1.0274912517;
    }

    void UpdateTime()
    {
        double mtc = (MarsSolDate % 1.0) * 24.0;
        LocalMeanSolarTime = ((mtc + Longitude / 15.0) % 24.0 + 24.0) % 24.0;
        Sol = (int)Math.Floor(MarsSolDate);

        double jdTT = MarsSolDate * 1.0274912517 + 2405522.0028779;
        double unix = (jdTT - 69.184 / 86400.0 - 2440587.5) * 86400.0;
        EarthUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);

        // Longitude solar (Ls) – Mars24 simplificado
        double dt = jdTT - 2451545.0;
        double M = Deg2Rad(19.3871 + 0.52402073 * dt);
        double alphaFMS = 270.3871 + 0.524038496 * dt;
        double eoc = (10.691 + 3.0e-7 * dt) * Math.Sin(M) + 0.623 * Math.Sin(2 * M)
                   + 0.050 * Math.Sin(3 * M) + 0.005 * Math.Sin(4 * M);
        SolarLongitude = (float)(((alphaFMS + eoc) % 360.0 + 360.0) % 360.0);
    }

    public string FormatLMST()
    {
        double h = LocalMeanSolarTime;
        int hh = (int)h;
        int mm = (int)((h - hh) * 60.0);
        int ss = (int)((((h - hh) * 60.0) - mm) * 60.0);
        return string.Format("{0:00}:{1:00}:{2:00}", hh, mm, ss);
    }

    public string SeasonName()
    {
        float ls = SolarLongitude;
        bool north = Latitude >= 0;
        string[] n = { "Primavera", "Verão", "Outono", "Inverno" };
        string[] s = { "Outono", "Inverno", "Primavera", "Verão" };
        int i = Mathf.Clamp((int)(ls / 90f), 0, 3);
        return (north ? n[i] : s[i]) + (north ? " (hem. norte)" : " (hem. sul)");
    }

    // ---------------------------------------------------------------- Posição
    void UpdatePosition()
    {
        Vector3 p = player != null ? player.position : Vector3.zero;
        double lat0 = Deg2Rad(referenceLatitude);
        Latitude = referenceLatitude + Rad2Deg(p.z / MarsRadius);
        Longitude = referenceLongitude + Rad2Deg(p.x / (MarsRadius * Math.Cos(lat0)));
        Elevation = p.y + elevationOffset - 1.0f; // aprox. altura dos pés
        if (player != null)
            HeadingDeg = (player.eulerAngles.y % 360f + 360f) % 360f;
    }

    public float ElevationAt(Vector3 worldPos)
    {
        float y = terrain != null ? terrain.SampleHeight(worldPos) + terrain.transform.position.y : worldPos.y;
        return y + elevationOffset;
    }

    // ----------------------------------------------------------- Sol e céu
    void UpdateSunAndSky()
    {
        double phi = Deg2Rad(Latitude);
        double ls = Deg2Rad(SolarLongitude);
        double decl = Math.Asin(0.42565 * Math.Sin(ls)) + Deg2Rad(0.25 * Math.Sin(ls));
        double h = Deg2Rad((LocalMeanSolarTime - 12.0) * 15.0);

        double east = -Math.Cos(decl) * Math.Sin(h);
        double north = Math.Cos(phi) * Math.Sin(decl) - Math.Sin(phi) * Math.Cos(decl) * Math.Cos(h);
        double up = Math.Sin(phi) * Math.Sin(decl) + Math.Cos(phi) * Math.Cos(decl) * Math.Cos(h);
        Vector3 sunDir = new Vector3((float)east, (float)up, (float)north).normalized;

        SunElevation = (float)Rad2Deg(Math.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)));
        SunAzimuth = (Mathf.Atan2(sunDir.x, sunDir.z) * Mathf.Rad2Deg + 360f) % 360f;

        double cosH0 = -Math.Tan(phi) * Math.Tan(decl);
        cosH0 = Math.Max(-1.0, Math.Min(1.0, cosH0));
        float h0 = (float)Rad2Deg(Math.Acos(cosH0)) / 15f;
        SunriseLMST = 12f - h0;
        SunsetLMST = 12f + h0;

        // Opacidade da poeira (tau)
        stormBlend = Mathf.MoveTowards(stormBlend, dustStorm ? 1f : 0f, Time.deltaTime * 0.15f);
        float season = (SolarLongitude > 180f && SolarLongitude < 330f) ? 0.35f : 0f; // estação de poeira
        float noise = Mathf.PerlinNoise((float)(MarsSolDate * 3.0 % 1000.0), 0.37f) * 0.25f;
        DustOpacity = Mathf.Lerp(0.45f + season + noise, 4.5f, stormBlend);

        // Irradiância na superfície (direta + difusa, estimativa)
        double nu = Deg2Rad(SolarLongitude - 251.0);
        float rAU = (float)(1.5236 * (1 - 0.0934 * 0.0934) / (1 + 0.0934 * Math.Cos(nu)));
        float toa = 1361f / (rAU * rAU);
        float sinE = Mathf.Max(0f, sunDir.y);
        float airmass = 1f / Mathf.Max(sinE, 0.05f);
        float direct = toa * sinE * Mathf.Exp(-DustOpacity * airmass * 0.9f);
        float diffuse = toa * sinE * 0.45f * (1f - Mathf.Exp(-DustOpacity * airmass * 0.9f));
        SolarIrradiance = direct + diffuse;

        // Luz do Sol
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            float day = Mathf.Clamp01(sunDir.y * 4f + 0.1f);
            sun.intensity = Mathf.Lerp(0f, 1.25f, day) * Mathf.Lerp(1f, 0.25f, stormBlend);
            sun.color = Color.Lerp(new Color(1f, 0.82f, 0.65f), new Color(1f, 0.95f, 0.88f), Mathf.Clamp01(sunDir.y * 2f));
            sun.shadowStrength = Mathf.Lerp(0.85f, 0.3f, stormBlend);
        }

        // Céu: caramelo de dia, azulado no pôr do sol (sim, em Marte o pôr do sol é azul)
        Color daySky = new Color(0.80f, 0.62f, 0.46f);
        Color duskSky = new Color(0.36f, 0.46f, 0.60f);
        Color nightSky = new Color(0.02f, 0.02f, 0.035f);
        Color stormSky = new Color(0.62f, 0.42f, 0.28f);
        Color sky;
        if (SunElevation > 12f) sky = daySky;
        else if (SunElevation > -2f) sky = Color.Lerp(duskSky, daySky, Mathf.InverseLerp(-2f, 12f, SunElevation));
        else sky = Color.Lerp(nightSky, duskSky, Mathf.InverseLerp(-12f, -2f, SunElevation));
        float bright = Mathf.Clamp01(Mathf.InverseLerp(-12f, 10f, SunElevation));
        sky = Color.Lerp(sky, stormSky * Mathf.Max(bright, 0.08f), stormBlend * 0.8f);

        if (cam != null) cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
        RenderSettings.fogDensity = 0.0004f + DustOpacity * 0.0007f + stormBlend * 0.006f;
        RenderSettings.ambientLight = Color.Lerp(new Color(0.03f, 0.03f, 0.05f), sky * 0.75f, bright);
    }

    // ------------------------------------------------------------- Atmosfera
    void UpdateAtmosphere()
    {
        double lmst = LocalMeanSolarTime;
        float seasonal = 1f + 0.09f * Mathf.Cos((SolarLongitude - 250f) * Mathf.Deg2Rad);  // ciclo do CO2 polar
        float diurnal = 1f + 0.035f * Mathf.Cos((float)(2.0 * Math.PI * (lmst - 6.0) / 24.0)); // maré térmica
        PressurePa = DatumPressure * Mathf.Exp(-Elevation / ScaleHeight) * seasonal * diurnal
                     * (1f + 0.02f * stormBlend);

        float meanT = -58f + 8f * Mathf.Cos((SolarLongitude - 250f) * Mathf.Deg2Rad);
        float phase = Mathf.Cos((float)(2.0 * Math.PI * (lmst - 14.0) / 24.0));
        float damp = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(DustOpacity / 4f)); // poeira reduz a amplitude
        AirTempC = meanT + 32f * damp * phase;
        GroundTempC = meanT + 6f + 48f * damp * phase;

        float t = (float)(MarsSolDate * 40.0 % 10000.0);
        WindSpeed = Mathf.Lerp(1.5f, 12f, Mathf.PerlinNoise(t, 1.7f)) + stormBlend * 18f;
        WindDirection = Mathf.PerlinNoise(t * 0.3f, 9.1f) * 360f;

        // RAD/Curiosity: ~0,67 mSv/sol ≈ 27 µSv/h. Menos atmosfera acima = mais dose.
        float shielding = Mathf.Exp(-(PressurePa - 750f) / 2500f);
        DoseRateMicroSvPerHour = 27f * shielding;
    }

    public static double Deg2Rad(double d) { return d * Math.PI / 180.0; }
    public static double Rad2Deg(double r) { return r * 180.0 / Math.PI; }

    public static string CardinalFromDegrees(float deg)
    {
        string[] dirs = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
        int i = Mathf.RoundToInt(((deg % 360f) + 360f) % 360f / 45f) % 8;
        return dirs[i];
    }

    public static string FormatHour(float h)
    {
        h = ((h % 24f) + 24f) % 24f;
        int hh = (int)h;
        int mm = (int)((h - hh) * 60f);
        return string.Format("{0:00}:{1:00}", hh, mm);
    }
}
