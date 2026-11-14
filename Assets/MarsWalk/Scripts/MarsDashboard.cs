using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD do capacete, painel de missão (TAB) e mapa em camadas (M).
/// Feito com IMGUI (OnGUI) para não precisar montar Canvas/prefabs: funciona
/// em qualquer projeto, em Built-in ou URP.
/// </summary>
public class MarsDashboard : MonoBehaviour
{
    [HideInInspector] public AstronautController player;
    [HideInInspector] public SuitSystems suit;
    [HideInInspector] public MarsEnvironment env;
    [HideInInspector] public Terrain terrain;

    [Header("Mapa")]
    public int mapResolution = 512;
    public float steepSlope = 25f;     // acima disso: perigoso
    public float cautionSlope = 15f;   // acima disso: atenção

    // Estado da interface
    bool showDashboard, showMap, showHelp = true, panelWasOpen;
    bool layerRelief = true, layerSlope = true, layerIce = true, layerMinerals = true, layerPOI = true, layerTrail = true, layerRange = true;
    PointOfInterest selectedPOI;
    bool hasTarget;
    Vector3 targetPos;
    string targetName = "";
    RouteInfo route;
    float routeTimer;

    // Rastro (breadcrumbs)
    readonly List<Vector3> trail = new List<Vector3>();

    // Texturas
    Texture2D texWhite, texDisk, texArrow, texMap, texSlope, texIce, texMinerals;
    GUIStyle sToggle, sTitle, sHeader, sText, sSmall, sValue, sButton, sCenter, sAlert;
    bool stylesReady;
    float S;        // escala da GUI
    float W, H;     // tamanho virtual da tela (altura = 1080)

    static readonly Color Accent = new Color(1f, 0.45f, 0.12f);
    static readonly Color PanelBg = new Color(0.04f, 0.05f, 0.07f, 0.86f);
    static readonly Color Ok = new Color(0.35f, 0.95f, 0.5f);
    static readonly Color Warn = new Color(1f, 0.8f, 0.2f);
    static readonly Color Bad = new Color(1f, 0.3f, 0.25f);

    public struct RouteInfo
    {
        public bool valid, safe;
        public float distance, maxSlope, climb, descent, realSeconds, simHours, batteryNeeded, o2RoundTrip;
        public string verdict;
        public List<string> issues;
    }

    // ================================================================ Ciclo
    void Start()
    {
        texWhite = Solid(Color.white);
        texDisk = MakeDisk(64);
        texArrow = MakeArrow(64);
        BuildMapTextures();
    }

    void Update()
    {
        if (MarsInput.Down(MarsKey.Dashboard)) { showDashboard = !showDashboard; if (showDashboard) showMap = false; }
        if (MarsInput.Down(MarsKey.Map)) { showMap = !showMap; if (showMap) showDashboard = false; }
        if (MarsInput.Down(MarsKey.Help)) showHelp = !showHelp;
        if (MarsInput.Down(MarsKey.Escape))
        {
            if (showDashboard || showMap) { showDashboard = false; showMap = false; }
            else if (player != null) player.SetCursorLocked(false);
        }

        bool panel = showDashboard || showMap;
        if (player != null)
        {
            player.lookEnabled = !panel;
            if (panel && !panelWasOpen) player.SetCursorLocked(false);
            if (!panel && panelWasOpen) player.SetCursorLocked(true);
        }
        panelWasOpen = panel;

        // Rastro
        if (player != null)
        {
            Vector3 p = player.transform.position;
            if (trail.Count == 0 || (trail[trail.Count - 1] - p).sqrMagnitude > 25f)
            {
                trail.Add(p);
                if (trail.Count > 3000) trail.RemoveAt(0);
            }
        }

        // Recalcula a rota 2x por segundo
        routeTimer -= Time.deltaTime;
        if (hasTarget && routeTimer <= 0f && player != null)
        {
            route = AnalyzeRoute(player.transform.position, targetPos);
            routeTimer = 0.5f;
        }
    }

    // ================================================================ GUI
    void OnGUI()
    {
        if (env == null || suit == null || player == null) return;
        InitStyles();
        S = Screen.height / 1080f;
        W = Screen.width / S; H = 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1f));

        if (!showMap) DrawWorldMarkers();
        DrawHUD();
        if (showDashboard) DrawDashboard();
        if (showMap) DrawMap();
    }

    // ---------------------------------------------------------------- HUD
    void DrawHUD()
    {
        // Canto superior esquerdo: tempo e posição
        Box(new Rect(20, 20, 430, 92), PanelBg);
        Box(new Rect(20, 20, 4, 92), Accent);
        GUI.Label(new Rect(36, 26, 420, 30), "SOL " + env.Sol + "  ·  " + env.FormatLMST() + " LMST", sHeader);
        GUI.Label(new Rect(36, 56, 420, 24), string.Format("{0}  {1:0.0000}°N  {2:0.0000}°L", env.siteName, env.Latitude, env.Longitude), sSmall);
        GUI.Label(new Rect(36, 80, 420, 24), string.Format("Elev. {0:0} m · {1:0} Pa · {2:0} °C", env.Elevation, env.PressurePa, env.AirTempC), sSmall);

        DrawCompass();

        // Canto inferior esquerdo: traje
        float y = H - 150;
        Box(new Rect(20, y, 360, 130), PanelBg);
        Box(new Rect(20, y, 4, 130), Accent);
        GUI.Label(new Rect(36, y + 8, 200, 24), "TRAJE EVA", sSmall);
        Bar(new Rect(36, y + 36, 250, 18), suit.oxygenHours / suit.maxOxygenHours, suit.oxygenHours < 1f ? Bad : new Color(0.4f, 0.75f, 1f));
        GUI.Label(new Rect(294, y + 32, 90, 24), "O2 " + suit.oxygenHours.ToString("0.0") + "h", sSmall);
        Bar(new Rect(36, y + 64, 250, 18), suit.battery / 100f, suit.battery < 20f ? Bad : Ok);
        GUI.Label(new Rect(294, y + 60, 90, 24), "BAT " + suit.battery.ToString("0") + "%", sSmall);
        GUI.Label(new Rect(36, y + 92, 340, 24), string.Format("Dose {0:0.000} mSv · {1:0.0} km/h · Lanterna {2}",
            suit.doseMilliSv, player.CurrentSpeed * 3.6f, suit.flashlightOn ? "ON" : "off"), sSmall);

        // Estação / recarga
        if (suit.CurrentStation != null)
            ShadowLabel(new Rect(W / 2 - 300, H - 230, 600, 30), "Conectado a " + suit.CurrentStation.displayName + " — recarregando", sCenter, Ok);

        // Alertas (direita)
        float ay = 20;
        foreach (var a in suit.Alerts)
        {
            bool crit = a.StartsWith("!!");
            Box(new Rect(W - 470, ay, 450, 34), new Color(crit ? 0.45f : 0.3f, crit ? 0.05f : 0.18f, 0.02f, 0.85f));
            GUI.Label(new Rect(W - 460, ay + 5, 440, 26), "! " + a.Replace("!! ", ""), sAlert);
            ay += 40;
        }

        // Destino
        if (hasTarget)
        {
            Vector3 p = player.transform.position;
            float dist = Flat(targetPos - p).magnitude;
            float bearing = Bearing(p, targetPos);
            Color c = route.valid ? (route.safe ? Ok : Warn) : Color.white;
            ShadowLabel(new Rect(W / 2 - 350, 88, 700, 28), string.Format("► {0} — {1:0} m · rumo {2:000}° ({3})",
                targetName, dist, bearing, MarsEnvironment.CardinalFromDegrees(bearing)), sCenter, c);
            if (dist < 12f) ShadowLabel(new Rect(W / 2 - 300, 118, 600, 28), "Destino alcançado!", sCenter, Ok);
        }

        // Ajuda
        if (showHelp)
        {
            ShadowLabel(new Rect(0, H - 40, W, 30),
                "WASD andar · Shift correr · Espaço pular · TAB painel · M mapa · F lanterna · G tempestade · 1/2 tempo (×" +
                env.timeScale.ToString("0") + ") · R voltar à base · H ocultar ajuda", sCenter, new Color(1f, 1f, 1f, 0.85f));
        }

        // Mira
        if (!showDashboard && !showMap)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(W / 2 - 3, H / 2 - 3, 6, 6), texDisk);
            GUI.color = Color.white;
        }
    }

    void DrawCompass()
    {
        float cx = W / 2f, width = 640f, y = 22f;
        Box(new Rect(cx - width / 2, y, width, 56), PanelBg);
        float heading = env.HeadingDeg;
        int start = Mathf.FloorToInt((heading - 90f) / 15f) * 15;
        for (int d = start; d <= heading + 90f; d += 15)
        {
            float off = Mathf.DeltaAngle(heading, d);
            if (Mathf.Abs(off) > 88f) continue;
            float x = cx + off / 90f * (width / 2f);
            int dn = ((d % 360) + 360) % 360;
            bool major = dn % 45 == 0;
            Box(new Rect(x - 1, y + 4, 2, major ? 14 : 8), new Color(1f, 1f, 1f, major ? 0.9f : 0.5f));
            if (major)
            {
                GUI.color = dn == 0 ? Accent : Color.white;
                GUI.Label(new Rect(x - 30, y + 18, 60, 26), MarsEnvironment.CardinalFromDegrees(dn), sCenter);
                GUI.color = Color.white;
            }
        }
        Box(new Rect(cx - 1, y, 2, 56), Accent);
        GUI.Label(new Rect(cx - 40, y + 36, 80, 22), heading.ToString("000") + "°", sCenter);

        if (hasTarget)
        {
            float off = Mathf.DeltaAngle(heading, Bearing(player.transform.position, targetPos));
            float x = cx + Mathf.Clamp(off, -88f, 88f) / 90f * (width / 2f);
            GUI.color = Ok;
            GUI.DrawTexture(new Rect(x - 7, y + 40, 14, 14), texDisk);
            GUI.color = Color.white;
        }
    }

    void DrawWorldMarkers()
    {
        Camera cam = env.cam;
        if (cam == null) return;
        Vector3 pp = player.transform.position;
        foreach (var poi in PointOfInterest.All)
        {
            Vector3 wp = poi.transform.position + Vector3.up * 3f;
            float dist = Vector3.Distance(pp, poi.transform.position);
            if (dist > 1800f) continue;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z <= 0f) continue;
            float x = sp.x / S, y = (Screen.height - sp.y) / S;
            Color c = PointOfInterest.ColorFor(poi.type);
            GUI.color = c;
            GUI.DrawTexture(new Rect(x - 6, y - 6, 12, 12), texDisk);
            GUI.color = Color.white;
            float alpha = Mathf.Lerp(1f, 0.55f, dist / 1800f);
            ShadowLabel(new Rect(x - 150, y + 6, 300, 22), poi.displayName + "  " + FormatDist(dist), sCenter, new Color(c.r, c.g, c.b, alpha));
        }
        if (hasTarget)
        {
            Vector3 sp = cam.WorldToScreenPoint(targetPos + Vector3.up * 2f);
            if (sp.z > 0f)
            {
                float x = sp.x / S, y = (Screen.height - sp.y) / S;
                GUI.color = Ok;
                GUI.DrawTexture(new Rect(x - 10, y - 30, 20, 20), texArrow);
                GUI.color = Color.white;
            }
        }
    }

    // ---------------------------------------------------------- Dashboard
    void DrawDashboard()
    {
        float pw = Mathf.Min(1560f, W - 60f), ph = 900f;
        Rect panel = new Rect((W - pw) / 2f, (H - ph) / 2f, pw, ph);
        Box(panel, new Color(0.02f, 0.03f, 0.05f, 0.93f));
        Box(new Rect(panel.x, panel.y, panel.width, 4), Accent);
        GUI.Label(new Rect(panel.x + 28, panel.y + 16, 900, 40), "PAINEL DE MISSÃO · MARSWALK", sTitle);
        GUI.Label(new Rect(panel.x + pw - 520, panel.y + 24, 490, 30),
            "Terra (UTC): " + env.EarthUtc.ToString("dd/MM/yyyy HH:mm"), sValueRight());

        float gx = panel.x + 24, gy = panel.y + 76, gap = 18f;
        float cw = (pw - 48 - gap * 2) / 3f, ch = 330f;
        Vector3 pp = player.transform.position;

        // 1. Tempo
        Card(new Rect(gx, gy, cw, ch), "TEMPO", new[]
        {
            Row("Sol (Mars Sol Date)", env.Sol.ToString()),
            Row("Hora local (LMST)", env.FormatLMST()),
            Row("Estação", env.SeasonName()),
            Row("Longitude solar (Ls)", env.SolarLongitude.ToString("0.0") + "°"),
            Row("Nascer / pôr do sol", MarsEnvironment.FormatHour(env.SunriseLMST) + " / " + MarsEnvironment.FormatHour(env.SunsetLMST)),
            Row("Duração do sol", "24h 39m 35s"),
            Row("Aceleração do tempo", "×" + env.timeScale.ToString("0")),
        });

        // 2. Localização
        float slope = SlopeAt(pp);
        Card(new Rect(gx + cw + gap, gy, cw, ch), "LOCALIZAÇÃO", new[]
        {
            Row("Local", env.siteName),
            Row("Latitude", env.Latitude.ToString("0.00000") + "° N"),
            Row("Longitude", env.Longitude.ToString("0.00000") + "° L"),
            Row("Elevação (MOLA)", env.Elevation.ToString("0") + " m"),
            Row("Rumo", env.HeadingDeg.ToString("000") + "° " + MarsEnvironment.CardinalFromDegrees(env.HeadingDeg)),
            Row("Declividade do solo", Colorize(slope.ToString("0") + "°", slope > steepSlope ? Bad : slope > cautionSlope ? Warn : Ok)),
            Row("Distância da base", FormatDist(DistanceToNearest(POIType.Habitat))),
        });

        // 3. Atmosfera
        Card(new Rect(gx + (cw + gap) * 2, gy, cw, ch), "ATMOSFERA", new[]
        {
            Row("Pressão", env.PressurePa.ToString("0") + " Pa (" + (env.PressurePa / 100f).ToString("0.00") + " mbar)"),
            Row("Temp. do ar", env.AirTempC.ToString("0") + " °C"),
            Row("Temp. do solo", env.GroundTempC.ToString("0") + " °C"),
            Row("Vento", env.WindSpeed.ToString("0.0") + " m/s de " + MarsEnvironment.CardinalFromDegrees(env.WindDirection)),
            Row("Opacidade da poeira τ", Colorize(env.DustOpacity.ToString("0.00"), env.DustOpacity > 2f ? Bad : env.DustOpacity > 1f ? Warn : Ok)),
            Row("Composição", "CO2 95% · N2 2,8% · Ar 2%"),
            Row("Gravidade", "3,72 m/s² (0,38 g)"),
        });

        float gy2 = gy + ch + gap;

        // 4. Sol e radiação
        Card(new Rect(gx, gy2, cw, ch), "SOL E RADIAÇÃO", new[]
        {
            Row("Elevação do Sol", env.SunElevation.ToString("0.0") + "°"),
            Row("Azimute do Sol", env.SunAzimuth.ToString("000") + "° " + MarsEnvironment.CardinalFromDegrees(env.SunAzimuth)),
            Row("Irradiância solar", env.SolarIrradiance.ToString("0") + " W/m²"),
            Row("Taxa de dose", env.DoseRateMicroSvPerHour.ToString("0.0") + " µSv/h"),
            Row("Dose nesta EVA", suit.doseMilliSv.ToString("0.000") + " mSv"),
            Row("Referência (RAD)", "≈ 0,67 mSv/sol"),
            Row("Limite p/ painéis", env.SolarIrradiance > 150f ? Colorize("gerando", Ok) : Colorize("baixa geração", Warn)),
        });

        // 5. Traje
        Rect suitRect = new Rect(gx + cw + gap, gy2, cw, ch);
        Card(suitRect, "TRAJE (EVA)", new[]
        {
            Row("Oxigênio", suit.oxygenHours.ToString("0.0") + " h de " + suit.maxOxygenHours.ToString("0") + " h"),
            Row("Bateria", suit.battery.ToString("0") + " %"),
            Row("Consumo atual", suit.CurrentDrainPerHour.ToString("0.0") + " %/h"),
            Row("Alcance a pé", FormatDist(suit.RangeMeters)),
            Row("Tempo de EVA", suit.EvaHours.ToString("0.0") + " h"),
            Row("Velocidade", (player.CurrentSpeed * 3.6f).ToString("0.0") + " km/h"),
            Row("Status", suit.Alerts.Count == 0 ? Colorize("NOMINAL", Ok) : Colorize(suit.Alerts.Count + " alerta(s)", Warn)),
        });

        // 6. Recursos e navegação
        var nearCharge = Nearest(pp, POIType.ChargingStation);
        var nearIce = Nearest(pp, POIType.WaterIce);
        var nearSafe = NearestRefill(pp);
        Card(new Rect(gx + (cw + gap) * 2, gy2, cw, ch), "RECURSOS E NAVEGAÇÃO", new[]
        {
            Row("Recarga mais próxima", DescribePOI(nearCharge, pp)),
            Row("O2 mais próximo", DescribePOI(nearSafe, pp)),
            Row("Gelo de água", DescribePOI(nearIce, pp)),
            Row("Destino", hasTarget ? targetName : "— (defina no mapa)"),
            Row("Rota", hasTarget && route.valid ? Colorize(route.verdict, route.safe ? Ok : Warn) : "—"),
            Row("Declive máx. na rota", hasTarget && route.valid ? route.maxSlope.ToString("0") + "°" : "—"),
            Row("Bateria p/ ida", hasTarget && route.valid ? route.batteryNeeded.ToString("0") + " %" : "—"),
        });

        // Rodapé
        float fy = panel.yMax - 74;
        GUI.Label(new Rect(panel.x + 28, fy, pw - 520, 60),
            "<b>Fontes (modeladas):</b> MOLA/MGS (relevo), HiRISE·CTX·CRISM·MCS·SHARAD/MRO, THEMIS/Odyssey, " +
            "REMS·RAD/Curiosity, MEDA·SHERLOC·PIXL/Perseverance. Valores ambientais são estimativas baseadas em médias publicadas.", sSmall);
        if (GUI.Button(new Rect(panel.xMax - 470, fy + 6, 220, 46), "Abrir mapa (M)", sButton)) { showDashboard = false; showMap = true; }
        if (GUI.Button(new Rect(panel.xMax - 240, fy + 6, 212, 46), env.dustStorm ? "Encerrar tempestade" : "Simular tempestade", sButton)) env.dustStorm = !env.dustStorm;
    }

    void Card(Rect r, string title, string[][] rows)
    {
        Box(r, PanelBg);
        Box(new Rect(r.x, r.y, r.width, 3), Accent);
        GUI.Label(new Rect(r.x + 18, r.y + 12, r.width - 36, 30), title, sHeader);
        float y = r.y + 54;
        foreach (var row in rows)
        {
            GUI.Label(new Rect(r.x + 18, y, r.width * 0.5f, 34), row[0], sText);
            GUI.Label(new Rect(r.x + r.width * 0.42f, y, r.width * 0.58f - 18, 34), row[1], sValue);
            Box(new Rect(r.x + 18, y + 35, r.width - 36, 1), new Color(1f, 1f, 1f, 0.07f));
            y += 38;
        }
    }

    static string[] Row(string k, string v) { return new[] { k, v }; }

    // ---------------------------------------------------------------- Mapa
    void DrawMap()
    {
        Rect panel = new Rect(30, 30, W - 60, H - 60);
        Box(panel, new Color(0.02f, 0.03f, 0.05f, 0.95f));
        Box(new Rect(panel.x, panel.y, panel.width, 4), Accent);
        GUI.Label(new Rect(panel.x + 24, panel.y + 14, 900, 40), "MAPA EM CAMADAS · " + env.siteName.ToUpper(), sTitle);

        float size = panel.height - 90;
        Rect map = new Rect(panel.x + 24, panel.y + 66, size, size);

        // Camadas
        Box(map, new Color(0.1f, 0.07f, 0.05f, 1f));
        if (layerRelief && texMap != null) GUI.DrawTexture(map, texMap);
        if (layerSlope && texSlope != null) GUI.DrawTexture(map, texSlope);
        if (layerIce && texIce != null) GUI.DrawTexture(map, texIce);
        if (layerMinerals && texMinerals != null) GUI.DrawTexture(map, texMinerals);

        // Grade com coordenadas
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = terrain.terrainData.size;
        for (float g = Mathf.Ceil(tPos.x / 500f) * 500f; g <= tPos.x + tSize.x; g += 500f)
        {
            Vector2 a = WorldToMap(map, new Vector3(g, 0, tPos.z));
            Box(new Rect(a.x, map.y, 1, map.height), new Color(1f, 1f, 1f, 0.12f));
            double lon = env.referenceLongitude + MarsEnvironment.Rad2Deg(g / (MarsEnvironment.MarsRadius * System.Math.Cos(MarsEnvironment.Deg2Rad(env.referenceLatitude))));
            GUI.Label(new Rect(a.x + 3, map.yMax - 22, 120, 20), lon.ToString("0.000") + "°L", sSmall);
        }
        for (float g = Mathf.Ceil(tPos.z / 500f) * 500f; g <= tPos.z + tSize.z; g += 500f)
        {
            Vector2 a = WorldToMap(map, new Vector3(tPos.x, 0, g));
            Box(new Rect(map.x, a.y, map.width, 1), new Color(1f, 1f, 1f, 0.12f));
            double lat = env.referenceLatitude + MarsEnvironment.Rad2Deg(g / MarsEnvironment.MarsRadius);
            GUI.Label(new Rect(map.x + 4, a.y - 20, 120, 20), lat.ToString("0.000") + "°N", sSmall);
        }

        Vector3 pp = player.transform.position;
        Vector2 pm = WorldToMap(map, pp);

        // Alcance da bateria
        if (layerRange)
        {
            float rangePx = suit.RangeMeters / tSize.x * map.width;
            DrawCircle(pm, rangePx, new Color(0.35f, 0.95f, 0.5f, 0.5f), 2f, map);
        }

        // Rastro
        if (layerTrail && trail.Count > 1)
            for (int i = 1; i < trail.Count; i++)
                DrawLine(WorldToMap(map, trail[i - 1]), WorldToMap(map, trail[i]), new Color(1f, 1f, 1f, 0.55f), 2f);

        // Rota até o destino
        if (hasTarget)
        {
            Color rc = route.valid ? (route.safe ? Ok : Warn) : Color.white;
            DrawLine(pm, WorldToMap(map, targetPos), rc, 3f);
            Vector2 tm = WorldToMap(map, targetPos);
            GUI.color = rc;
            GUI.DrawTexture(new Rect(tm.x - 9, tm.y - 9, 18, 18), texDisk);
            GUI.color = Color.white;
        }

        // Pontos de interesse
        if (layerPOI)
        {
            foreach (var poi in PointOfInterest.All)
            {
                Vector2 m = WorldToMap(map, poi.transform.position);
                Color c = PointOfInterest.ColorFor(poi.type);
                float rpx = Mathf.Max(5f, poi.radius / tSize.x * map.width);
                if (poi.type == POIType.Hazard) DrawCircle(m, rpx, Bad, 2f, map);
                float s = poi == selectedPOI ? 24f : 16f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(m.x - s / 2 - 2, m.y - s / 2 - 2, s + 4, s + 4), texDisk);
                GUI.color = c;
                GUI.DrawTexture(new Rect(m.x - s / 2, m.y - s / 2, s, s), texDisk);
                GUI.color = Color.white;
                ShadowLabel(new Rect(m.x + 12, m.y - 11, 300, 22), poi.displayName, sSmall, c);
            }
        }

        // Astronauta
        Matrix4x4 baseM = GUI.matrix;
        GUI.matrix = baseM * Matrix4x4.TRS(new Vector3(pm.x, pm.y, 0f), Quaternion.Euler(0f, 0f, env.HeadingDeg), Vector3.one);
        GUI.color = Accent;
        GUI.DrawTexture(new Rect(-13, -13, 26, 26), texArrow);
        GUI.color = Color.white;
        GUI.matrix = baseM;

        // Norte e escala
        GUI.Label(new Rect(map.xMax - 50, map.y + 8, 40, 30), "N ▲", sHeader);
        float px500 = 500f / tSize.x * map.width;
        Box(new Rect(map.xMax - 20 - px500, map.yMax - 44, px500, 4), Color.white);
        GUI.Label(new Rect(map.xMax - 20 - px500, map.yMax - 70, px500, 24), "500 m", sCenter);

        // Clique no mapa: seleciona POI ou cria ponto de rota
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && map.Contains(e.mousePosition))
        {
            PointOfInterest hit = null;
            float best = 16f;
            foreach (var poi in PointOfInterest.All)
            {
                float d = Vector2.Distance(e.mousePosition, WorldToMap(map, poi.transform.position));
                if (d < best) { best = d; hit = poi; }
            }
            if (hit != null) { selectedPOI = hit; SetTarget(hit.transform.position, hit.displayName); }
            else
            {
                selectedPOI = null;
                Vector3 w = MapToWorld(map, e.mousePosition);
                double lat = env.referenceLatitude + MarsEnvironment.Rad2Deg(w.z / MarsEnvironment.MarsRadius);
                SetTarget(w, "Ponto " + lat.ToString("0.0000") + "°N");
            }
            e.Use();
        }

        DrawMapSidebar(new Rect(map.xMax + 24, map.y, panel.xMax - map.xMax - 48, map.height));
    }

    void DrawMapSidebar(Rect r)
    {
        float y = r.y;
        GUI.Label(new Rect(r.x, y, r.width, 30), "CAMADAS DE DADOS", sHeader); y += 36;
        layerRelief = Toggle(r.x, ref y, r.width, layerRelief, "Relevo sombreado", "MOLA (MGS) / DTM HiRISE (MRO)");
        layerSlope = Toggle(r.x, ref y, r.width, layerSlope, "Declividade / áreas de risco", "Derivado do DTM · >" + steepSlope + "° vermelho, >" + cautionSlope + "° amarelo");
        layerIce = Toggle(r.x, ref y, r.width, layerIce, "Gelo subsuperficial", "SHARAD (MRO) · SWIM");
        layerMinerals = Toggle(r.x, ref y, r.width, layerMinerals, "Minerais hidratados", "CRISM (MRO)");
        layerPOI = Toggle(r.x, ref y, r.width, layerPOI, "Pontos de interesse", "Bases, estações, alvos");
        layerTrail = Toggle(r.x, ref y, r.width, layerTrail, "Trajeto percorrido", "Registro da EVA");
        layerRange = Toggle(r.x, ref y, r.width, layerRange, "Alcance da bateria", "Círculo verde = ida possível a pé");

        y += 8;
        GUI.Label(new Rect(r.x, y, r.width, 30), "LEGENDA", sHeader); y += 34;
        foreach (POIType t in System.Enum.GetValues(typeof(POIType)))
        {
            GUI.color = PointOfInterest.ColorFor(t);
            GUI.DrawTexture(new Rect(r.x + 2, y + 5, 14, 14), texDisk);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 24, y, r.width - 24, 24), PointOfInterest.Label(t), sSmall);
            y += 24;
        }

        y += 12;
        GUI.Label(new Rect(r.x, y, r.width, 30), "PLANEJAMENTO DE ROTA", sHeader); y += 34;
        if (!hasTarget)
        {
            GUI.Label(new Rect(r.x, y, r.width, 60), "Clique em um ponto do mapa para traçar uma rota até ele.", sText);
            return;
        }

        if (selectedPOI != null)
        {
            GUI.Label(new Rect(r.x, y, r.width, 26), "<b>" + selectedPOI.displayName + "</b> · " + PointOfInterest.Label(selectedPOI.type), sText); y += 28;
            GUI.Label(new Rect(r.x, y, r.width, 46), selectedPOI.description, sSmall); y += 46;
            GUI.Label(new Rect(r.x, y, r.width, 40), "<i>Fonte: " + selectedPOI.dataSource + "</i>", sSmall); y += 44;
        }
        else
        {
            GUI.Label(new Rect(r.x, y, r.width, 26), "<b>" + targetName + "</b>", sText); y += 30;
        }

        if (route.valid)
        {
            GUI.color = route.safe ? Ok : Warn;
            GUI.Label(new Rect(r.x, y, r.width, 28), "● " + route.verdict, sHeader);
            GUI.color = Color.white; y += 32;
            GUI.Label(new Rect(r.x, y, r.width, 120), string.Format(
                "Distância: {0}\nSubida / descida: +{1:0} m / −{2:0} m\nDeclive máximo: {3:0}°\nTempo a pé: {4:0.0} h (sim.)\nBateria (ida): {5:0}% · O2 (ida e volta): {6:0.0} h",
                FormatDist(route.distance), route.climb, route.descent, route.maxSlope, route.simHours, route.batteryNeeded, route.o2RoundTrip), sSmall);
            y += 118;
            if (route.issues != null)
                foreach (var issue in route.issues) { GUI.Label(new Rect(r.x, y, r.width, 22), "• " + issue, sSmall); y += 22; }
        }

        if (GUI.Button(new Rect(r.x, Mathf.Min(y + 10, r.yMax - 50), r.width, 44), "Limpar destino", sButton))
        { hasTarget = false; selectedPOI = null; }
    }

    bool Toggle(float x, ref float y, float w, bool value, string label, string sub)
    {
        bool v = GUI.Toggle(new Rect(x, y, w, 26), value, " " + label, sToggle);
        GUI.Label(new Rect(x + 26, y + 24, w - 26, 20), sub, sSmall);
        y += 50;
        return v;
    }

    void SetTarget(Vector3 pos, string name)
    {
        hasTarget = true;
        targetPos = pos;
        targetName = name;
        route = AnalyzeRoute(player.transform.position, targetPos);
    }

    // ------------------------------------------------------- Análise de rota
    RouteInfo AnalyzeRoute(Vector3 from, Vector3 to)
    {
        var info = new RouteInfo { valid = true, issues = new List<string>() };
        Vector3 flat = Flat(to - from);
        int steps = Mathf.Max(2, Mathf.CeilToInt(flat.magnitude / 8f));
        float lastH = GroundY(from);
        var hazards = new HashSet<PointOfInterest>();
        for (int i = 1; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
            float h = GroundY(p);
            float dh = h - lastH;
            if (dh > 0) info.climb += dh; else info.descent -= dh;
            info.distance += Mathf.Sqrt(dh * dh + (flat.magnitude / steps) * (flat.magnitude / steps));
            lastH = h;
            info.maxSlope = Mathf.Max(info.maxSlope, SlopeAt(p));
            foreach (var poi in PointOfInterest.All)
                if (poi.type == POIType.Hazard && Flat(poi.transform.position - p).magnitude < poi.radius)
                    hazards.Add(poi);
        }

        info.realSeconds = info.distance / Mathf.Max(0.1f, player.walkSpeed) * (1f + info.climb / Mathf.Max(1f, info.distance) * 3f);
        info.simHours = info.realSeconds * suit.SimHoursPerRealSecond;
        info.batteryNeeded = info.simHours * (suit.drainPerHour + (suit.flashlightOn ? suit.flashlightPerHour : 0f));
        info.o2RoundTrip = info.simHours * 2f;

        if (info.maxSlope > steepSlope) info.issues.Add("Declive de " + info.maxSlope.ToString("0") + "° no caminho");
        foreach (var hz in hazards) info.issues.Add("Atravessa: " + hz.displayName);
        if (info.batteryNeeded > suit.battery) info.issues.Add("Bateria insuficiente para a ida");
        if (info.o2RoundTrip > suit.oxygenHours) info.issues.Add("O2 insuficiente para ida e volta");
        if (env.DustOpacity > 2f) info.issues.Add("Tempestade de poeira ativa");

        info.safe = info.issues.Count == 0;
        info.verdict = info.safe ? "ROTA SEGURA" : (info.issues.Count == 1 ? "ROTA COM RISCO" : "ROTA NÃO RECOMENDADA");
        return info;
    }

    // ------------------------------------------------- Texturas do mapa
    void BuildMapTextures()
    {
        if (terrain == null) return;
        var td = terrain.terrainData;
        int n = mapResolution;
        texMap = NewTex(n); texSlope = NewTex(n); texIce = NewTex(n); texMinerals = NewTex(n);
        var cMap = new Color[n * n]; var cSlope = new Color[n * n]; var cIce = new Color[n * n]; var cMin = new Color[n * n];

        float minH = float.MaxValue, maxH = float.MinValue;
        var heights = new float[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float h = td.GetInterpolatedHeight(x / (n - 1f), y / (n - 1f));
                heights[y * n + x] = h;
                minH = Mathf.Min(minH, h); maxH = Mathf.Max(maxH, h);
            }

        Vector3 lightDir = new Vector3(-1f, 1.4f, 1f).normalized; // luz de noroeste
        Color low = new Color(0.30f, 0.15f, 0.09f), mid = new Color(0.72f, 0.42f, 0.24f), high = new Color(0.95f, 0.80f, 0.62f);
        Vector3 tPos = terrain.transform.position, tSize = td.size;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f), v = y / (n - 1f);
                int i = y * n + x;
                float t = Mathf.InverseLerp(minH, maxH, heights[i]);
                Color baseC = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, high, (t - 0.5f) * 2f);
                Vector3 nrm = td.GetInterpolatedNormal(u, v);
                float shade = Mathf.Clamp01(Vector3.Dot(nrm, lightDir)) * 0.75f + 0.35f;
                Color c = baseC * shade; c.a = 1f;
                cMap[i] = c;

                float steep = td.GetSteepness(u, v);
                if (steep > steepSlope) cSlope[i] = new Color(1f, 0.1f, 0.05f, 0.55f);
                else if (steep > cautionSlope) cSlope[i] = new Color(1f, 0.85f, 0.1f, 0.3f);
                else cSlope[i] = new Color(0, 0, 0, 0);

                Vector3 w = new Vector3(tPos.x + u * tSize.x, 0f, tPos.z + v * tSize.z);
                float ice = Field(w, POIType.WaterIce, 190f) * (0.6f + 0.4f * Mathf.PerlinNoise(u * 18f, v * 18f));
                cIce[i] = new Color(0.3f, 0.7f, 1f, Mathf.Clamp01(ice) * 0.55f);
                float min = Field(w, POIType.Minerals, 230f) * (0.5f + 0.5f * Mathf.PerlinNoise(u * 25f + 7f, v * 25f));
                cMin[i] = new Color(0.3f, 1f, 0.45f, Mathf.Clamp01(min) * 0.5f);
            }
        }
        texMap.SetPixels(cMap); texMap.Apply();
        texSlope.SetPixels(cSlope); texSlope.Apply();
        texIce.SetPixels(cIce); texIce.Apply();
        texMinerals.SetPixels(cMin); texMinerals.Apply();
    }

    float Field(Vector3 w, POIType type, float sigma)
    {
        float f = 0f;
        foreach (var poi in PointOfInterest.All)
        {
            if (poi.type != type) continue;
            float d = Flat(poi.transform.position - w).magnitude;
            f += Mathf.Exp(-(d * d) / (2f * sigma * sigma));
        }
        return f;
    }

    // ---------------------------------------------------------- Utilidades
    Vector2 WorldToMap(Rect map, Vector3 w)
    {
        Vector3 tPos = terrain.transform.position, tSize = terrain.terrainData.size;
        float u = (w.x - tPos.x) / tSize.x, v = (w.z - tPos.z) / tSize.z;
        return new Vector2(map.x + u * map.width, map.y + (1f - v) * map.height);
    }

    Vector3 MapToWorld(Rect map, Vector2 m)
    {
        Vector3 tPos = terrain.transform.position, tSize = terrain.terrainData.size;
        float u = (m.x - map.x) / map.width, v = 1f - (m.y - map.y) / map.height;
        var w = new Vector3(tPos.x + u * tSize.x, 0f, tPos.z + v * tSize.z);
        w.y = GroundY(w);
        return w;
    }

    float GroundY(Vector3 p) { return terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y; }

    float SlopeAt(Vector3 p)
    {
        if (terrain == null) return 0f;
        Vector3 tPos = terrain.transform.position, tSize = terrain.terrainData.size;
        float u = Mathf.Clamp01((p.x - tPos.x) / tSize.x), v = Mathf.Clamp01((p.z - tPos.z) / tSize.z);
        return terrain.terrainData.GetSteepness(u, v);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static float Bearing(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
    }

    PointOfInterest Nearest(Vector3 p, POIType type)
    {
        PointOfInterest best = null; float bd = float.MaxValue;
        foreach (var poi in PointOfInterest.All)
        {
            if (poi.type != type) continue;
            float d = Flat(poi.transform.position - p).magnitude;
            if (d < bd) { bd = d; best = poi; }
        }
        return best;
    }

    PointOfInterest NearestRefill(Vector3 p)
    {
        PointOfInterest best = null; float bd = float.MaxValue;
        foreach (var poi in PointOfInterest.All)
        {
            if (!poi.RefillsOxygen) continue;
            float d = Flat(poi.transform.position - p).magnitude;
            if (d < bd) { bd = d; best = poi; }
        }
        return best;
    }

    float DistanceToNearest(POIType type)
    {
        var poi = Nearest(player.transform.position, type);
        return poi == null ? 0f : Flat(poi.transform.position - player.transform.position).magnitude;
    }

    string DescribePOI(PointOfInterest poi, Vector3 p)
    {
        if (poi == null) return "—";
        float d = Flat(poi.transform.position - p).magnitude;
        float b = Bearing(p, poi.transform.position);
        return FormatDist(d) + " " + MarsEnvironment.CardinalFromDegrees(b);
    }

    static string FormatDist(float m) { return m >= 1000f ? (m / 1000f).ToString("0.00") + " km" : m.ToString("0") + " m"; }

    static string Colorize(string s, Color c) { return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + s + "</color>"; }

    void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, texWhite);
        GUI.color = Color.white;
    }

    void Bar(Rect r, float v, Color c)
    {
        Box(r, new Color(1f, 1f, 1f, 0.1f));
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), c);
    }

    void ShadowLabel(Rect r, string text, GUIStyle style, Color c)
    {
        GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        GUI.color = c;
        GUI.Label(r, text, style);
        GUI.color = Color.white;
    }

    void DrawLine(Vector2 a, Vector2 b, Color c, float width)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.5f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Matrix4x4 m = GUI.matrix;
        GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, ang), Vector3.one);
        GUI.color = c;
        GUI.DrawTexture(new Rect(0f, -width / 2f, len, width), texWhite);
        GUI.color = Color.white;
        GUI.matrix = m;
    }

    void DrawCircle(Vector2 center, float radius, Color c, float width, Rect clip)
    {
        const int seg = 64;
        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            if (clip.Contains(p) && clip.Contains(prev)) DrawLine(prev, p, c, width);
            prev = p;
        }
    }

    static Texture2D NewTex(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c); t.Apply();
        return t;
    }

    static Texture2D MakeDisk(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float r = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }
        t.Apply();
        return t;
    }

    // Seta apontando para cima (norte)
    static Texture2D MakeArrow(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Vector2 tip = new Vector2(n * 0.5f, n * 0.95f), l = new Vector2(n * 0.12f, n * 0.08f), r = new Vector2(n * 0.88f, n * 0.08f), notch = new Vector2(n * 0.5f, n * 0.3f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = InTri(p, tip, l, notch) || InTri(p, tip, notch, r);
                t.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        t.Apply();
        return t;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    GUIStyle sValueRightCache;
    GUIStyle sValueRight()
    {
        if (sValueRightCache == null)
        {
            sValueRightCache = new GUIStyle(sValue);
            sValueRightCache.alignment = TextAnchor.MiddleRight;
        }
        return sValueRightCache;
    }

    void InitStyles()
    {
        if (stylesReady) return;
        stylesReady = true;
        sText = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, wordWrap = true };
        sText.normal.textColor = new Color(0.85f, 0.85f, 0.88f);
        sSmall = new GUIStyle(sText) { fontSize = 15 };
        sSmall.normal.textColor = new Color(0.78f, 0.78f, 0.82f);
        sValue = new GUIStyle(sText) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        sValue.normal.textColor = Color.white;
        sHeader = new GUIStyle(sText) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = false };
        sHeader.normal.textColor = Color.white;
        sTitle = new GUIStyle(sHeader) { fontSize = 30 };
        sTitle.normal.textColor = Accent;
        sCenter = new GUIStyle(sText) { alignment = TextAnchor.UpperCenter, wordWrap = false, fontSize = 17 };
        sCenter.normal.textColor = Color.white;
        sAlert = new GUIStyle(sText) { fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = false };
        sAlert.normal.textColor = Color.white;
        sButton = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        sToggle = new GUIStyle(GUI.skin.toggle) { fontSize = 19 };
        sToggle.normal.textColor = sToggle.onNormal.textColor = sToggle.hover.textColor = sToggle.onHover.textColor = Color.white;
    }
}
