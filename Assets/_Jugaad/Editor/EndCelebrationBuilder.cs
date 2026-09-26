#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Adds the end-of-game celebration (confetti, applause, light flicker, finger-press MAIN MENU button)
/// to the game scene. Safe to re-run. Menu: Jugaad > Add End Celebration
/// </summary>
public static class EndCelebrationBuilder
{
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Jugaad/Add End Celebration")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }
        var old = GameObject.Find("EndCelebration");
        if (old) Object.DestroyImmediate(old);

        var root = new GameObject("EndCelebration");
        var cel = root.AddComponent<EndCelebration>();

        // ---------- confetti ----------
        var cgo = new GameObject("Confetti");
        cgo.transform.SetParent(root.transform, false);
        var ps = cgo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.45f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 350;
        var colors = new Gradient();
        colors.SetKeys(new[]
        {
            new GradientColorKey(new Color(0.95f, 0.30f, 0.30f), 0f),
            new GradientColorKey(new Color(0.98f, 0.74f, 0.20f), 0.25f),
            new GradientColorKey(new Color(0.35f, 0.85f, 0.45f), 0.5f),
            new GradientColorKey(new Color(0.30f, 0.60f, 0.98f), 0.75f),
            new GradientColorKey(new Color(0.85f, 0.40f, 0.95f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        var sc = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
        main.startColor = sc;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 220), new ParticleSystem.Burst(0.25f, 100) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.15f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.8f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 1.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;

        var pr = cgo.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = ConfettiMaterial();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        // ---------- MAIN MENU poke button (same poke setup as the wall switches) ----------
        var src = GameObject.Find("Button_Light");
        if (src == null) { Debug.LogError("[EndCelebrationBuilder] Button_Light not found (poke template)."); return; }
        var btn = Object.Instantiate(src);
        btn.name = "MainMenuButton";
        btn.transform.SetParent(root.transform, false);
        btn.transform.localPosition = Vector3.zero;
        btn.transform.localRotation = Quaternion.identity;
        btn.transform.localScale = Vector3.one;
        var toggle = btn.GetComponent<PokeToggleButton>();
        if (toggle) Object.DestroyImmediate(toggle);
        var cap = btn.transform.Find("Cap");
        var led = cap ? cap.Find("LED") : null;
        if (led) Object.DestroyImmediate(led.gameObject);

        var box = btn.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, 0f, 0.01f);
        box.size = new Vector3(0.18f, 0.08f, 0.03f);
        if (cap)
        {
            cap.localPosition = new Vector3(0f, 0f, 0.012f);
            cap.localRotation = Quaternion.identity;
            cap.localScale = new Vector3(0.18f, 0.08f, 0.024f);
            var r = cap.GetComponent<MeshRenderer>();
            if (r) r.sharedMaterial = ButtonMaterial();
        }
        // backing plate
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Plate";
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.transform.SetParent(btn.transform, false);
        plate.transform.localPosition = new Vector3(0f, 0.012f, -0.004f);
        plate.transform.localScale = new Vector3(0.22f, 0.15f, 0.012f);
        plate.GetComponent<MeshRenderer>().sharedMaterial = PlateMaterial();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Label(btn.transform, "Label", "MAIN MENU", font, 0.23f, new Color(0.12f, 0.08f, 0.04f), new Vector3(0f, 0f, 0.0245f), new Vector2(0.17f, 0.07f), FontStyles.Bold);
        Label(btn.transform, "Hint", "Ungli se dabao", font, 0.17f, Color.white, new Vector3(0f, 0.062f, 0.004f), new Vector2(0.2f, 0.03f), FontStyles.Normal);

        var pab = btn.AddComponent<PokeActionButton>();
        var pso = new SerializedObject(pab);
        pso.FindProperty("cap").objectReferenceValue = cap;
        pso.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(pab.onPressed, new UnityAction(cel.GoToMainMenu));

        // ---------- wiring ----------
        var so = new SerializedObject(cel);
        so.FindProperty("flow").objectReferenceValue = Object.FindFirstObjectByType<GameFlowManager>();
        so.FindProperty("confetti").objectReferenceValue = ps;
        so.FindProperty("menuButton").objectReferenceValue = btn;
        so.ApplyModifiedPropertiesWithoutUndo();
        btn.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EndCelebrationBuilder] End celebration added.");
    }

    static void Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
                      Vector3 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from the +Z (player) side
        var t = go.AddComponent<TextMeshPro>();
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = box;
    }

    static Material ConfettiMaterial()
    {
        const string path = "Assets/_Jugaad/Materials/Confetti.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ButtonMaterial()
    {
        const string path = "Assets/_Jugaad/Materials/MenuButton_Orange.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(0.95f, 0.64f, 0.23f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.35f, 0.2f, 0.05f));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PlateMaterial()
    {
        const string path = "Assets/_Jugaad/Materials/MenuButton_Plate.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.17f));
        EditorUtility.SetDirty(m);
        return m;
    }
}
#endif
