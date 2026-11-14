using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// COMO USAR: crie um GameObject vazio na cena, adicione este componente e aperte Play.
/// Ele monta tudo sozinho: terreno, Sol, astronauta, câmera, pontos de interesse,
/// ambiente marciano, traje e o dashboard.
///
/// Se já existir um Terrain na cena (ex.: um DTM real importado), ele é usado no lugar
/// do terreno procedural.
/// </summary>
public class MarsBootstrap : MonoBehaviour
{
    [Header("Terreno")]
    public bool useExistingTerrainIfPresent = true;
    public int seed = 7;
    public float terrainSize = 2000f;
    public float terrainMaxHeight = 220f;

    [Header("Pontos de interesse")]
    public bool spawnDefaultPOIs = true;

    [Header("Material dos objetos (evita objetos rosa)")]
    [Tooltip("Crie um Material padrão (Assets > Create > Material) e arraste aqui")]
    public Material baseMaterial;
    static Material sharedBase;

    class POIDef
    {
        public string name; public POIType type; public float x, z, radius; public string desc, source;
        public POIDef(string n, POIType t, float x, float z, float r, string d, string s)
        { name = n; type = t; this.x = x; this.z = z; radius = r; desc = d; source = s; }
    }

    // Layout de exemplo (posições em metros a partir do centro do mapa)
    static readonly POIDef[] DefaultPOIs =
    {
        new POIDef("Base Alfa (Habitat)", POIType.Habitat, 0f, 30f, 14f,
            "Habitat pressurizado. Reabastece O2 e bateria do traje.",
            "Local escolhido com imagens HiRISE/CTX (MRO) e relevo MOLA (MGS)"),
        new POIDef("Estação Solar 1", POIType.ChargingStation, 340f, 180f, 9f,
            "Painéis solares + bateria. Recarrega o traje.",
            "Insolação estimada com MCS (MRO) e REMS/MEDA (Curiosity/Perseverance)"),
        new POIDef("Estação Solar 2", POIType.ChargingStation, -430f, 360f, 9f,
            "Painéis solares + bateria. Recarrega o traje.",
            "Insolação estimada com MCS (MRO) e REMS/MEDA"),
        new POIDef("Estação Solar 3", POIType.ChargingStation, 160f, -520f, 9f,
            "Painéis solares + bateria. Recarrega o traje.",
            "Insolação estimada com MCS (MRO) e REMS/MEDA"),
        new POIDef("Gelo subsuperficial", POIType.WaterIce, -620f, -310f, 25f,
            "Gelo de água provável a < 1 m de profundidade. Recurso para água, O2 e combustível (ISRU).",
            "Radar SHARAD (MRO), projeto SWIM, crateras recentes vistas pela HiRISE"),
        new POIDef("Argilas e carbonatos (delta)", POIType.Minerals, 520f, 620f, 30f,
            "Minerais formados em água no antigo delta. Bons alvos para bioassinaturas.",
            "Espectrômetro CRISM (MRO); SuperCam/PIXL (Perseverance)"),
        new POIDef("Afloramento sedimentar", POIType.ScienceTarget, -260f, 640f, 15f,
            "Rochas em camadas: coletar amostra para retorno à Terra.",
            "Mastcam-Z, SHERLOC e PIXL (Perseverance)"),
        new POIDef("Estação meteorológica", POIType.ScienceTarget, 120f, 90f, 8f,
            "Sensores de pressão, temperatura, vento e poeira.",
            "Baseado em REMS (Curiosity) e MEDA (Perseverance)"),
        new POIDef("Zona de pouso de contingência", POIType.SafeZone, 720f, -220f, 20f,
            "Área plana e segura com abrigo e O2 de emergência.",
            "Declividade derivada de DTM HiRISE; rochas mapeadas pela HiRISE"),
        new POIDef("Campo de rochas", POIType.Hazard, -160f, -260f, 35f,
            "Blocos de até 2 m. Evite atravessar a pé.",
            "Densidade de rochas a partir de imagens HiRISE (MRO)"),
        new POIDef("Dunas instáveis", POIType.Hazard, 380f, -300f, 45f,
            "Areia fofa: risco de atolar e de queda.",
            "Imagens HiRISE/CTX e inércia térmica THEMIS (Mars Odyssey)"),
    };

    void Awake()
    {
        sharedBase = baseMaterial != null ? baseMaterial : FindFallbackMaterial();
        // 1) Terreno
        Terrain terrain = useExistingTerrainIfPresent ? Terrain.activeTerrain : null;
        if (terrain == null)
        {
            var flat = new List<Vector3>();
            foreach (var p in DefaultPOIs)
                if (p.type != POIType.Hazard) flat.Add(new Vector3(p.x, p.z, p.radius + 6f));
            flat.Add(new Vector3(0f, 0f, 30f)); // ponto de partida
            terrain = MarsTerrainGenerator.Create(seed, terrainSize, terrainMaxHeight, flat);
        }

        // 2) Desliga câmera/luz padrão da cena
        var oldCam = Camera.main;
        if (oldCam != null) oldCam.gameObject.SetActive(false);
#if UNITY_2022_2_OR_NEWER
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        var lights = FindObjectsOfType<Light>();
#endif
        foreach (var l in lights)
            if (l.type == LightType.Directional) l.gameObject.SetActive(false);

        // 3) Sol
        var sunGO = new GameObject("Sol (Mars)");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        // 4) Astronauta
        Vector3 spawn = new Vector3(0f, GroundY(terrain, 0f, 0f) + 1.2f, 0f);
        var player = new GameObject("Astronauta");
        player.transform.position = spawn;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0f, 0f);
        cc.slopeLimit = 40f; cc.stepOffset = 0.4f;

        var camGO = new GameObject("Câmera do capacete");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(player.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.fieldOfView = 70f;
        camGO.AddComponent<AudioListener>();

        var lampGO = new GameObject("Lanterna");
        lampGO.transform.SetParent(camGO.transform, false);
        var lamp = lampGO.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 45f; lamp.spotAngle = 55f; lamp.intensity = 2.5f;
        lamp.color = new Color(1f, 0.97f, 0.9f);
        lamp.enabled = false;

        var ctrl = player.AddComponent<AstronautController>();
        ctrl.cameraPivot = camGO.transform;

        // 5) Ambiente
        var env = gameObject.AddComponent<MarsEnvironment>();
        env.sun = sun; env.cam = cam; env.player = player.transform; env.terrain = terrain;

        // 6) Traje
        var suit = player.AddComponent<SuitSystems>();
        suit.flashlight = lamp;

        // 7) Pontos de interesse
        if (spawnDefaultPOIs)
            foreach (var p in DefaultPOIs) SpawnPOI(terrain, p);

        // 8) Dashboard
        var dash = gameObject.AddComponent<MarsDashboard>();
        dash.player = ctrl; dash.suit = suit; dash.env = env; dash.terrain = terrain;
    }

    static float GroundY(Terrain t, float x, float z)
    {
        var p = new Vector3(x, 0f, z);
        return t.SampleHeight(p) + t.transform.position.y;
    }

    void SpawnPOI(Terrain terrain, POIDef d)
    {
        float y = GroundY(terrain, d.x, d.z);
        var root = new GameObject(d.name);
        root.transform.position = new Vector3(d.x, y, d.z);
        var poi = root.AddComponent<PointOfInterest>();
        poi.displayName = d.name; poi.type = d.type; poi.radius = d.radius;
        poi.description = d.desc; poi.dataSource = d.source;

        Color c = PointOfInterest.ColorFor(d.type);

        if (d.type != POIType.Hazard)
        {
            // Farol (sem colisão) visível de longe
            var beacon = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 4f, 0f), new Vector3(0.25f, 4f, 0.25f), c, true);
            Destroy(beacon.GetComponent<Collider>());
            var lightGO = new GameObject("Luz");
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 8.5f, 0f);
            var pl = lightGO.AddComponent<Light>();
            pl.type = LightType.Point; pl.color = c; pl.range = 18f; pl.intensity = 2f;
        }

        switch (d.type)
        {
            case POIType.Habitat:
                Prim(PrimitiveType.Sphere, root.transform, new Vector3(-6f, 0f, 6f), new Vector3(10f, 7f, 10f), new Color(0.9f, 0.9f, 0.88f), false);
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(2f, 1.5f, 6f), new Vector3(3f, 1.5f, 3f), new Color(0.85f, 0.85f, 0.85f), false);
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(10f, 0.05f, 10f), new Color(0.35f, 0.35f, 0.38f), false);
                break;
            case POIType.ChargingStation:
                for (int i = -1; i <= 1; i++)
                {
                    var panel = Prim(PrimitiveType.Cube, root.transform, new Vector3(i * 2.2f, 1.4f, 3f), new Vector3(2f, 0.06f, 3f), new Color(0.08f, 0.12f, 0.3f), false);
                    panel.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, root.transform, new Vector3(i * 2.2f, 0.6f, 3f), new Vector3(0.1f, 0.6f, 0.1f), Color.gray, false);
                }
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1f, 1.2f, 0.6f), new Color(0.8f, 0.8f, 0.8f), false);
                break;
            case POIType.WaterIce:
                for (int i = 0; i < 6; i++)
                {
                    Vector3 o = new Vector3(Random.Range(-8f, 8f), 0.1f, Random.Range(-8f, 8f));
                    Prim(PrimitiveType.Sphere, root.transform, o, new Vector3(2.2f, 0.4f, 2.2f), new Color(0.85f, 0.93f, 1f), false);
                }
                break;
            case POIType.Hazard:
                for (int i = 0; i < 40; i++)
                {
                    Vector2 r2 = Random.insideUnitCircle * d.radius;
                    float s = Random.Range(0.4f, 2.2f);
                    var pos = new Vector3(d.x + r2.x, 0f, d.z + r2.y);
                    var rockGO = Prim(PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(s * 1.3f, s * 0.8f, s), new Color(0.28f, 0.18f, 0.13f), false);
                    rockGO.transform.position = new Vector3(pos.x, GroundY(terrain, pos.x, pos.z) + s * 0.2f, pos.z);
                    rockGO.transform.rotation = Random.rotation;
                }
                break;
            default:
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f), c * 0.8f, false);
                break;
        }
    }

    // Procura um shader compatível com o pipeline atual (URP ou Built-in)
    static Material FindFallbackMaterial()
    {
        bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        string[] names = urp
            ? new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit" }
            : new[] { "Standard", "Legacy Shaders/Diffuse" };
        foreach (var n in names)
        {
            var sh = Shader.Find(n);
            if (sh != null) return new Material(sh);
        }
        return null;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool emissive)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var mr = go.GetComponent<Renderer>();
        if (sharedBase != null) mr.material = new Material(sharedBase);
        mr.material.color = color;
        if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", color);
        if (emissive)
        {
            mr.material.EnableKeyword("_EMISSION");
            mr.material.SetColor("_EmissionColor", color * 2f);
        }
        return go;
    }
}
