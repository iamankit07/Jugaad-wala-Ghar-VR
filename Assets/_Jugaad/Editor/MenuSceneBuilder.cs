#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds (or rebuilds) Assets/Scenes/MainMenu.unity: XR rig with ray pointers, a world-space
/// menu with START / INFO / CONTROLS, and puts it first in Build Settings.
/// Menu: Jugaad > Build Main Menu Scene
/// </summary>
public static class MenuSceneBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string GuidePath = "Assets/_Jugaad/UI/Controller_Guide.png";

    static readonly Color Orange = new Color(0.95f, 0.64f, 0.23f);
    static readonly Color OrangeHi = new Color(1f, 0.76f, 0.40f);
    static readonly Color OrangeDown = new Color(0.78f, 0.50f, 0.16f);
    static readonly Color Dark = new Color(0.09f, 0.10f, 0.14f, 0.94f);
    static readonly Color Btn = new Color(0.20f, 0.22f, 0.29f);
    static readonly Color BtnHi = new Color(0.30f, 0.33f, 0.42f);
    static readonly Color TextMain = new Color(0.94f, 0.94f, 0.96f);
    static readonly Color TextDim = new Color(0.65f, 0.67f, 0.73f);

    static TMP_FontAsset _font;
    static Sprite _round;

    [MenuItem("Jugaad/Build Main Menu Scene")]
    public static void Build()
    {
        if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---------- lighting / environment ----------
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.35f, 0.33f, 0.40f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.20f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.10f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.92f, 0.82f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(6f, 0.01f, 6f);
        floor.transform.position = new Vector3(0f, -0.01f, 0f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = FloorMaterial();
        floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // ---------- XR ----------
        new GameObject("XR Interaction Manager").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();

        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.transform.position = Vector3.zero;
        // stay in front of the menu: no walking / turning / teleporting here
        foreach (var p in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
            p.enabled = false;
        var cam = rig.GetComponentInChildren<Camera>(true);
        if (cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.10f);
            cam.tag = "MainCamera";
        }

        // ---------- menu ----------
        var menuGo = new GameObject("MenuManager");
        var menu = menuGo.AddComponent<MainMenu>();

        var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        canvasGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(1600f, 1000f);
        crt.localScale = Vector3.one * 0.001f;          // 1 unit = 1 mm -> 1.6 m x 1.0 m
        crt.position = new Vector3(0f, 1.45f, 2.0f);

        var main = MakePanel(crt, "MainPanel");
        MakeText(main, "Title", "JUGAAD WALA GHAR", 120, Orange, new Vector2(0, 330), new Vector2(1500, 150), FontStyles.Bold);
        MakeText(main, "Subtitle", "Ghar ka har kaam... jugaad se!", 46, TextMain, new Vector2(0, 225), new Vector2(1400, 70), FontStyles.Italic);
        var start = MakeButton(main, "StartButton", "START", new Vector2(0, 70), new Vector2(560, 130), 64, true);
        var info = MakeButton(main, "InfoButton", "INFO", new Vector2(0, -90), new Vector2(560, 110), 50, false);
        var ctrl = MakeButton(main, "ControlsButton", "CONTROLS", new Vector2(0, -225), new Vector2(560, 110), 50, false);
        MakeText(main, "Footer", "Point karo + TRIGGER dabao      |      Game Jam  -  Theme: JUGAAD      |      Made by Ankit Kumar",
             28, TextDim, new Vector2(0, -410), new Vector2(1500, 50), FontStyles.Normal);

        var infoP = MakePanel(crt, "InfoPanel");
        MakeText(infoP, "Title", "GAME KE BAARE MEIN", 76, Orange, new Vector2(0, 390), new Vector2(1500, 100), FontStyles.Bold);
        var body = MakeText(infoP, "Body",
            "Is ghar mein sab kuch kharab hai - aur tumhe har kaam <b>JUGAAD</b> se karna hai.\n\n" +
            "- TV ka remote toota hai, darwaze pe tala hai, chaabi kho gayi hai...\n" +
            "- Aas-paas ki cheezon se kaam chalao: safety pin, rubber band, magnet, kitaab...\n" +
            "- Kul <b>7 tasks</b> hain. Jitna jaldi karoge, utne zyada points (har task 1000 tak).\n" +
            "- Atak gaye? Kuch der baad <b>HINT</b> aayega - par us task ke points 20% kam.\n" +
            "- End mein milega tumhara rank: Naya Jugaadu, Pakka Jugaadu ya <b>JUGAAD MASTER!</b>",
            38, TextMain, new Vector2(0, 20), new Vector2(1380, 560), FontStyles.Normal);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 12f;
        var infoBack = MakeButton(infoP, "BackButton", "BACK", new Vector2(0, -400), new Vector2(360, 100), 46, false);

        var ctrlP = MakePanel(crt, "ControlsPanel");
        MakeText(ctrlP, "Title", "CONTROLS", 76, Orange, new Vector2(0, 410), new Vector2(1500, 100), FontStyles.Bold);
        var imgGo = new GameObject("ControllerGuide", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(ctrlP, false);
        var img = imgGo.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GuidePath);
        img.preserveAspect = true;
        img.raycastTarget = false;
        var irt = (RectTransform)imgGo.transform;
        irt.anchoredPosition = new Vector2(0, 130);
        irt.sizeDelta = new Vector2(1000, 475);
        var list = MakeText(ctrlP, "List",
            "<color=#5AA5FF><b>GRIP</b></color> (side button) - cheezein pakdo, darwaze ka handle kheecho\n" +
            "<color=#F2A33A><b>TRIGGER</b></color> - haath mein pakde remote ka button dabao (TV / Fan)\n" +
            "<color=#7CE68C><b>LEFT STICK</b></color> - chalo        <color=#7CE68C><b>RIGHT STICK</b></color> - ghoomo\n" +
            "<b>UNGLI</b> - switchboard ke button ko chhu ke dabao",
            32, TextMain, new Vector2(0, -225), new Vector2(1380, 200), FontStyles.Normal);
        list.alignment = TextAlignmentOptions.Top;
        list.lineSpacing = 10f;
        var ctrlBack = MakeButton(ctrlP, "BackButton", "BACK", new Vector2(0, -410), new Vector2(360, 100), 46, false);

        var loadP = MakePanel(crt, "LoadingPanel");
        MakeText(loadP, "Loading", "Ghar khul raha hai...", 72, Orange, Vector2.zero, new Vector2(1400, 120), FontStyles.Bold);

        // ---------- wiring ----------
        var so = new SerializedObject(menu);
        so.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
        so.FindProperty("infoPanel").objectReferenceValue = infoP.gameObject;
        so.FindProperty("controlsPanel").objectReferenceValue = ctrlP.gameObject;
        so.FindProperty("loadingPanel").objectReferenceValue = loadP.gameObject;
        so.FindProperty("gameScene").stringValue = "SampleScene";
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(start.onClick, new UnityAction(menu.StartGame));
        UnityEventTools.AddPersistentListener(info.onClick, new UnityAction(menu.ShowInfo));
        UnityEventTools.AddPersistentListener(ctrl.onClick, new UnityAction(menu.ShowControls));
        UnityEventTools.AddPersistentListener(infoBack.onClick, new UnityAction(menu.ShowMain));
        UnityEventTools.AddPersistentListener(ctrlBack.onClick, new UnityAction(menu.ShowMain));

        infoP.gameObject.SetActive(false);
        ctrlP.gameObject.SetActive(false);
        loadP.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        Debug.Log("[MenuSceneBuilder] Built " + ScenePath + " and set Build Settings order (MainMenu, SampleScene).");
    }

    static Material FloorMaterial()
    {
        const string path = "Assets/_Jugaad/Materials/Menu_Floor.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.18f));
        m.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static RectTransform MakePanel(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = _round; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.25f;
        img.color = Dark;
        return rt;
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, Color color,
                                Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize, bool primary)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = _round; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.35f;
        img.color = Color.white; // tint comes from the ColorBlock
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = primary ? Orange : Btn;
        cb.highlightedColor = primary ? OrangeHi : BtnHi;
        cb.selectedColor = cb.normalColor;
        cb.pressedColor = primary ? OrangeDown : new Color(0.14f, 0.15f, 0.20f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        var t = MakeText(rt, "Label", label, fontSize, primary ? new Color(0.12f, 0.08f, 0.04f) : TextMain,
                     Vector2.zero, size, FontStyles.Bold);
        t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = Vector2.zero;
        return b;
    }
}
#endif
