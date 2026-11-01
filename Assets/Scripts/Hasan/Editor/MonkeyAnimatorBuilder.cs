#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// CANOPY: The Last Run - Monkey_B1.Fbx Otomatik Model Değiştirici ve AnimatorController Oluşturucu.
/// Yeni model (Assets/Models/Hasan/FbxUnity/FbxUnity/Monkey_B1.Fbx) içindeki:
/// - Idle, Idle2, Run, RunR, Jump, Jump2, Jump3, ClimbIdle, ClimbSit, SitClimb, Sit
/// animasyonlarını otomatik eşleştirir:
///   • Dala tutunma (Hang) -> ClimbIdle
///   • Dalda sallanma (Swing) -> ClimbSit
///   • Daldan dala geçiş (Transfer) -> SitClimb
///   • Duvarda koşma (WallRunLeft / WallRunRight) -> Run / RunR
///   • Kayma (Slide) -> Sit / ClimbSit
/// Ayrıca sahnedeki eski 'maymun' objesini otomatik olarak 'Monkey_B1' ile değiştirir ve "Eye" mesh'ini bağlar.
/// </summary>
[InitializeOnLoad]
public static class MonkeyAnimatorBuilder
{
    private const string DefaultControllerPath = "Assets/Animations/MonkeyController.controller";
    private const string MonkeyB1FbxPath = "Assets/Models/Hasan/FbxUnity/FbxUnity/Monkey_B1.Fbx";
    private const string FallbackOldFbxPath = "Assets/Scripts/Hasan/maymun/maymun.fbx";
    private const float FastTransitionDuration = 0.12f;

    static MonkeyAnimatorBuilder()
    {
        EditorApplication.delayCall += AutoSetupMonkeyB1AndController;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            AutoSetupMonkeyB1AndController();
        }
    }

    /// <summary>
    /// Unity'ye dönüldüğünde veya Play'e basıldığında otomatik olarak:
    /// 1) Monkey_B1.Fbx içindeki döngüsel kliplerin Loop Time ayarını açar,
    /// 2) Sahnedeki eski 'maymun' modelini 'Monkey_B1' ile değiştirir,
    /// 3) MonkeyController.controller'ı yeni kliplerle (ClimbIdle, ClimbSit, Run, Jump vb.) oluşturup bağlar.
    /// </summary>
    private static void AutoSetupMonkeyB1AndController()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        string fbxPath = FindMonkeyFbxPath();
        if (string.IsNullOrEmpty(fbxPath))
            return;

        EnsureLoopTimeOnModelClips(fbxPath);
        EnsureScenePlayerUsesMonkeyB1(fbxPath, false);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DefaultControllerPath);
        bool needsRebuild = (controller == null) || !DoesControllerUseCurrentFbx(controller, fbxPath);

        if (needsRebuild && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            BuildAnimatorControllerInternal(false);
        }
        else if (controller != null)
        {
            AssignControllerToScenePlayer(controller, null, false);
        }
    }

    private static bool DoesControllerUseCurrentFbx(AnimatorController controller, string fbxPath)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
            return false;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        if (sm == null || sm.defaultState == null || sm.defaultState.motion == null)
            return false;

        string motionPath = AssetDatabase.GetAssetPath(sm.defaultState.motion);
        return string.Equals(motionPath, fbxPath, StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Tools/Monkey/Build Animator Controller")]
    public static void BuildAnimatorControllerMenu()
    {
        string fbxPath = FindMonkeyFbxPath();
        if (!string.IsNullOrEmpty(fbxPath))
        {
            EnsureLoopTimeOnModelClips(fbxPath);
            EnsureScenePlayerUsesMonkeyB1(fbxPath, true);
        }
        BuildAnimatorControllerInternal(true);
    }

    [MenuItem("Tools/Monkey/Replace Scene Model With Monkey_B1")]
    public static void ReplaceSceneModelMenu()
    {
        string fbxPath = FindMonkeyFbxPath();
        if (string.IsNullOrEmpty(fbxPath))
        {
            Debug.LogError("[MonkeyAnimatorBuilder] Monkey_B1.Fbx bulunamadı!");
            return;
        }

        EnsureLoopTimeOnModelClips(fbxPath);
        EnsureScenePlayerUsesMonkeyB1(fbxPath, true);
        BuildAnimatorControllerInternal(true);
        FitCharacterControllerMenu();
    }

    /// <summary>
    /// Monkey_B1.Fbx içindeki Idle, Run, RunR, ClimbIdle, ClimbSit, SitIdle, Sit kliplerinin
    /// Loop Time özelliğini ModelImporter üzerinden otomatik açar.
    /// </summary>
    private static void EnsureLoopTimeOnModelClips(string fbxPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        bool wasEmpty = (clips == null || clips.Length == 0);
        if (wasEmpty)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
            return;

        bool modified = false;
        for (int i = 0; i < clips.Length; i++)
        {
            string lower = clips[i].name.ToLowerInvariant();
            bool shouldLoop =
                lower.Contains("idle") ||
                lower.Contains("run") ||
                lower.Contains("walk") ||
                lower.Contains("climb") ||
                lower.Contains("sit") ||
                lower.Contains("jump2");

            // SitClimb tek seferlik tırmanma/geçiş ise loop olmasın, ancak ClimbIdle ve ClimbSit kesinlikle loop olsun
            if (lower == "sitclimb")
            {
                shouldLoop = false;
            }

            if (shouldLoop && (!clips[i].loopTime || clips[i].wrapMode != WrapMode.Loop))
            {
                clips[i].loopTime = true;
                clips[i].wrapMode = WrapMode.Loop;
                modified = true;
            }
        }

        if (modified)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log($"<b>[MonkeyAnimatorBuilder]</b> '{fbxPath}' içindeki döngüsel kliplerin (Idle, Run, ClimbIdle, ClimbSit vb.) <b>Loop Time</b> ayarı otomatik açıldı.");
        }
    }

    /// <summary>
    /// Sahnedeki Player altında eski 'maymun' modeli varsa onu otomatik olarak 'Monkey_B1' ile değiştirir.
    /// </summary>
    private static void EnsureScenePlayerUsesMonkeyB1(string fbxPath, bool verboseLog)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        PlayerMotor motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
        if (motor == null)
            return;

        GameObject monkeyB1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (monkeyB1Prefab == null)
            return;

        Transform existingB1 = null;
        Transform oldMaymun = null;

        for (int i = 0; i < motor.transform.childCount; i++)
        {
            Transform child = motor.transform.GetChild(i);
            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("monkey_b1"))
            {
                existingB1 = child;
            }
            else if (lower.Contains("maymun") || child.Find("Animal_3146") != null)
            {
                oldMaymun = child;
            }
        }

        if (existingB1 == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(monkeyB1Prefab, motor.transform);
            if (instance != null)
            {
                instance.name = "Monkey_B1";
                existingB1 = instance.transform;

                if (oldMaymun != null)
                {
                    existingB1.localPosition = oldMaymun.localPosition;
                    existingB1.localRotation = oldMaymun.localRotation;
                    existingB1.localScale = Vector3.one;

                    // Eski maymun objesinin üzerinde PlayerVisual veya MonkeyAnimationDriver varsa yeni modele taşı veya Player'a ekle
                    MoveHelperComponentsIfOnOldModel(oldMaymun.gameObject, existingB1.gameObject);
                    oldMaymun.gameObject.SetActive(false);
                    UnityEngine.Object.DestroyImmediate(oldMaymun.gameObject);
                }
                else
                {
                    CharacterController cc = motor.GetComponent<CharacterController>();
                    float bottomY = cc != null ? (cc.center.y - cc.height * 0.5f) : -1f;
                    existingB1.localPosition = new Vector3(0f, bottomY, 0f);
                    existingB1.localRotation = Quaternion.identity;
                    existingB1.localScale = Vector3.one;
                }

                if (verboseLog)
                {
                    Debug.Log("<b>[MonkeyAnimatorBuilder]</b> Sahnedeki Player modeli 'Monkey_B1' ile otomatik değiştirildi!");
                }
            }
        }
        else if (oldMaymun != null && oldMaymun.gameObject.activeSelf)
        {
            oldMaymun.gameObject.SetActive(false);
        }

        // PlayerVisual ve MonkeyAnimationDriver bileşenlerinin sahnede olduğundan emin ol
        PlayerVisual visual = motor.GetComponentInChildren<PlayerVisual>(true);
        if (visual == null && existingB1 != null)
        {
            visual = existingB1.gameObject.AddComponent<PlayerVisual>();
        }
        if (visual != null)
        {
            visual.ResolveReferences();
            visual.CacheModelRenderers();
            EditorUtility.SetDirty(visual);
        }

        MonkeyAnimationDriver driver = motor.GetComponentInChildren<MonkeyAnimationDriver>(true);
        if (driver == null && existingB1 != null)
        {
            driver = existingB1.gameObject.AddComponent<MonkeyAnimationDriver>();
        }
        if (driver != null)
        {
            driver.ResolveReferences();
            EditorUtility.SetDirty(driver);
        }
    }

    private static void MoveHelperComponentsIfOnOldModel(GameObject oldObj, GameObject newObj)
    {
        PlayerVisual oldVis = oldObj.GetComponent<PlayerVisual>();
        if (oldVis != null && newObj.GetComponent<PlayerVisual>() == null)
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(oldVis);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newObj);
        }

        MonkeyAnimationDriver oldDrv = oldObj.GetComponent<MonkeyAnimationDriver>();
        if (oldDrv != null && newObj.GetComponent<MonkeyAnimationDriver>() == null)
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(oldDrv);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newObj);
        }
    }

    private static void BuildAnimatorControllerInternal(bool verboseLog)
    {
        string fbxPath = FindMonkeyFbxPath();
        if (string.IsNullOrEmpty(fbxPath))
        {
            if (verboseLog)
            {
                Debug.LogError("[MonkeyAnimatorBuilder] Projede 'Monkey_B1.Fbx' bulunamadı!");
            }
            return;
        }

        UnityEngine.Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        List<AnimationClip> clips = new List<AnimationClip>();

        for (int i = 0; i < allSubAssets.Length; i++)
        {
            if (allSubAssets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            {
                clips.Add(clip);
            }
        }

        if (verboseLog)
        {
            StringBuilder scanLog = new StringBuilder();
            scanLog.AppendLine($"<b>[MonkeyAnimatorBuilder]</b> '{fbxPath}' tarandı. Bulunan toplam klip sayısı: <b>{clips.Count}</b>");
            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip c = clips[i];
                scanLog.AppendLine($"  [{i + 1}] Klip: '<b>{c.name}</b>' | Süre: {c.length:F2}s | Loop: {c.isLooping}");
            }
            Debug.Log(scanLog.ToString());
        }

        // Monkey_B1.Fbx içindeki özel klip isimlerini tam ve alt-string olarak eşleştir:
        // Bulunan klipler: Idle, Idle2, Run, RunR, Jump, Jump2, Jump3, ClimbIdle, ClimbSit, SitClimb, Sit, SitIdle, Smile, Roar, Roar2, Eat, Die
        HashSet<AnimationClip> usedClips = new HashSet<AnimationClip>();

        AnimationClip clipIdle = MatchExactOrContains(clips, usedClips, new[] { "idle", "idle2", "sitidle" }, "idle", "breath", "stand");
        AnimationClip clipRun = MatchExactOrContains(clips, usedClips, new[] { "run" }, "run", "walk");
        AnimationClip clipRunR = MatchExactOrContains(clips, usedClips, new[] { "runr", "sprint" }, "runr", "sprint", "run");
        AnimationClip clipJump = MatchExactOrContains(clips, usedClips, new[] { "jump" }, "jump");
        AnimationClip clipJump2 = MatchExactOrContains(clips, usedClips, new[] { "jump2" }, "jump2", "fall", "jump");
        AnimationClip clipJump3 = MatchExactOrContains(clips, usedClips, new[] { "jump3" }, "jump3", "land", "jump2", "jump");
        AnimationClip clipClimbIdle = MatchExactOrContains(clips, usedClips, new[] { "climbidle" }, "climbidle", "climb");
        AnimationClip clipClimbSit = MatchExactOrContains(clips, usedClips, new[] { "climbsit" }, "climbsit", "climbidle");
        AnimationClip clipSitClimb = MatchExactOrContains(clips, usedClips, new[] { "sitclimb" }, "sitclimb", "climbidle", "climbsit");
        AnimationClip clipSit = MatchExactOrContains(clips, usedClips, new[] { "sit", "sitidle" }, "sit", "slide");

        AnimationClip ultimateFallback = clipRun ?? clipIdle ?? (clips.Count > 0 ? clips[0] : null);

        // State -> Klip atamaları (Kullanıcının isteğine göre:
        // Dala tutunma -> ClimbIdle & ClimbSit; Duvarda koşma -> Run & RunR; Zıplama -> Jump / Jump2 / Jump3; Kayma -> Sit / ClimbSit)
        List<string> fallbackStates = new List<string>();
        AnimationClip finalIdle = ResolveWithFallback("Idle", clipIdle, fallbackStates, ultimateFallback);
        AnimationClip finalWalk = ResolveWithFallback("Walk (Blend)", clipRun, fallbackStates, clipRunR, finalIdle, ultimateFallback);
        AnimationClip finalRun = ResolveWithFallback("Run (Blend)", clipRun, fallbackStates, clipRunR, finalIdle, ultimateFallback);
        AnimationClip finalSprint = ResolveWithFallback("Sprint (Blend)", clipRunR, fallbackStates, clipRun, ultimateFallback);
        AnimationClip finalJumpUp = ResolveWithFallback("JumpUp", clipJump, fallbackStates, clipJump2, finalRun, ultimateFallback);
        AnimationClip finalFall = ResolveWithFallback("Fall", clipJump2, fallbackStates, clipJump, clipClimbIdle, ultimateFallback);
        AnimationClip finalLand = ResolveWithFallback("Land", clipJump3, fallbackStates, clipJump2, clipJump, finalIdle);
        AnimationClip finalSlide = ResolveWithFallback("Slide", clipSit, fallbackStates, clipClimbSit, finalRun, ultimateFallback);
        AnimationClip finalWallLeft = ResolveWithFallback("WallRunLeft", clipRun, fallbackStates, clipRunR, ultimateFallback);
        AnimationClip finalWallRight = ResolveWithFallback("WallRunRight", clipRunR, fallbackStates, clipRun, ultimateFallback);
        AnimationClip finalHang = ResolveWithFallback("Hang (Dala Tutunma)", clipClimbIdle, fallbackStates, clipClimbSit, finalFall);
        AnimationClip finalSwing = ResolveWithFallback("Swing (Dalda Sallanma)", clipClimbSit, fallbackStates, clipClimbIdle, finalHang);
        AnimationClip finalTransfer = ResolveWithFallback("Transfer (Daldan Dala)", clipSitClimb, fallbackStates, clipJump, clipClimbIdle, finalHang);

        EnsureDirectoryExists("Assets/Animations");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(DefaultControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(DefaultControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(DefaultControllerPath);

        controller.AddParameter(MonkeyAnimationDriver.ParamSpeed, AnimatorControllerParameterType.Float);
        controller.AddParameter(MonkeyAnimationDriver.ParamIsGrounded, AnimatorControllerParameterType.Bool);
        controller.AddParameter(MonkeyAnimationDriver.ParamVerticalVelocity, AnimatorControllerParameterType.Float);
        controller.AddParameter(MonkeyAnimationDriver.ParamState, AnimatorControllerParameterType.Int);
        controller.AddParameter(MonkeyAnimationDriver.ParamWallSide, AnimatorControllerParameterType.Float);
        controller.AddParameter(MonkeyAnimationDriver.ParamSwingVelocity, AnimatorControllerParameterType.Float);
        controller.AddParameter(MonkeyAnimationDriver.ParamAnimSpeedMultiplier, AnimatorControllerParameterType.Float);
        controller.AddParameter(MonkeyAnimationDriver.ParamJumpTrigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(MonkeyAnimationDriver.ParamLandTrigger, AnimatorControllerParameterType.Trigger);

        AnimatorControllerParameter[] ctrlParams = controller.parameters;
        for (int i = 0; i < ctrlParams.Length; i++)
        {
            if (ctrlParams[i].name == MonkeyAnimationDriver.ParamAnimSpeedMultiplier)
            {
                ctrlParams[i].defaultFloat = 1f;
            }
        }
        controller.parameters = ctrlParams;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState stateIdle = sm.AddState("Idle", new Vector3(260f, 0f, 0f));
        stateIdle.motion = finalIdle;
        sm.defaultState = stateIdle;

        AnimatorState stateRun = CreateRunBlendTreeState(controller, sm, finalWalk, finalRun, finalSprint, new Vector3(540f, 0f, 0f));
        stateRun.speedParameterActive = true;
        stateRun.speedParameter = MonkeyAnimationDriver.ParamAnimSpeedMultiplier;

        AnimatorState stateJumpUp = sm.AddState("JumpUp", new Vector3(260f, -140f, 0f));
        stateJumpUp.motion = finalJumpUp;

        AnimatorState stateFall = sm.AddState("Fall", new Vector3(540f, -140f, 0f));
        stateFall.motion = finalFall;

        AnimatorState stateLand = sm.AddState("Land", new Vector3(800f, -140f, 0f));
        stateLand.motion = finalLand;

        AnimatorState stateSlide = sm.AddState("Slide", new Vector3(540f, 120f, 0f));
        stateSlide.motion = finalSlide;

        AnimatorState stateWallLeft = sm.AddState("WallRunLeft", new Vector3(260f, 240f, 0f));
        stateWallLeft.motion = finalWallLeft;
        stateWallLeft.speedParameterActive = true;
        stateWallLeft.speedParameter = MonkeyAnimationDriver.ParamAnimSpeedMultiplier;

        AnimatorState stateWallRight = sm.AddState("WallRunRight", new Vector3(540f, 240f, 0f));
        stateWallRight.motion = finalWallRight;
        stateWallRight.speedParameterActive = true;
        stateWallRight.speedParameter = MonkeyAnimationDriver.ParamAnimSpeedMultiplier;

        AnimatorState stateHang = sm.AddState("Hang", new Vector3(260f, 360f, 0f));
        stateHang.motion = finalHang;
        stateHang.speedParameterActive = true;
        stateHang.speedParameter = MonkeyAnimationDriver.ParamAnimSpeedMultiplier;

        AnimatorState stateSwing = sm.AddState("Swing", new Vector3(540f, 360f, 0f));
        stateSwing.motion = finalSwing;
        stateSwing.speedParameterActive = true;
        stateSwing.speedParameter = MonkeyAnimationDriver.ParamAnimSpeedMultiplier;

        AnimatorState stateTransfer = sm.AddState("Transfer", new Vector3(800f, 360f, 0f));
        stateTransfer.motion = finalTransfer;

        ConfigureTransitions(
            sm,
            stateIdle,
            stateRun,
            stateJumpUp,
            stateFall,
            stateLand,
            stateSlide,
            stateWallLeft,
            stateWallRight,
            stateHang,
            stateSwing,
            stateTransfer
        );

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string unmatchedSummary =
            $"Model: Monkey_B1 | Idle:{GetShortClipName(finalIdle)} | Run:{GetShortClipName(finalRun)}/{GetShortClipName(finalSprint)} | " +
            $"Jump:{GetShortClipName(finalJumpUp)} | Hang:{GetShortClipName(finalHang)} | Swing:{GetShortClipName(finalSwing)}";

        if (verboseLog)
        {
            StringBuilder summaryLog = new StringBuilder();
            summaryLog.AppendLine($"<b>[MonkeyAnimatorBuilder] Monkey_B1 AnimatorController başarıyla oluşturuldu:</b> '{DefaultControllerPath}'");
            summaryLog.AppendLine("<b>--- MONKEY_B1 STATE -> KLİP EŞLEŞME ÖZETİ ---</b>");
            summaryLog.AppendLine($"  • Idle                    -> <b>{GetShortClipName(finalIdle)}</b>");
            summaryLog.AppendLine($"  • Run (BlendTree)         -> Walk: <b>{GetShortClipName(finalWalk)}</b> | Run: <b>{GetShortClipName(finalRun)}</b> | Sprint: <b>{GetShortClipName(finalSprint)}</b>");
            summaryLog.AppendLine($"  • JumpUp / Fall / Land    -> JumpUp: <b>{GetShortClipName(finalJumpUp)}</b> | Fall: <b>{GetShortClipName(finalFall)}</b> | Land: <b>{GetShortClipName(finalLand)}</b>");
            summaryLog.AppendLine($"  • Slide [C]               -> <b>{GetShortClipName(finalSlide)}</b>");
            summaryLog.AppendLine($"  • WallRunLeft / Right     -> Sol: <b>{GetShortClipName(finalWallLeft)}</b> | Sağ: <b>{GetShortClipName(finalWallRight)}</b>");
            summaryLog.AppendLine($"  • Hang (Dala Tutunma)     -> <b>{GetShortClipName(finalHang)}</b>");
            summaryLog.AppendLine($"  • Swing (Dalda Sallanma)  -> <b>{GetShortClipName(finalSwing)}</b>");
            summaryLog.AppendLine($"  • Transfer (Daldan Dala)  -> <b>{GetShortClipName(finalTransfer)}</b>");
            Debug.Log(summaryLog.ToString());
        }

        AssignControllerToScenePlayer(controller, unmatchedSummary, verboseLog);
    }

    [MenuItem("Tools/Monkey/Fit Character Controller")]
    public static void FitCharacterControllerMenu()
    {
        PlayerMotor motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
        if (motor == null)
        {
            Debug.LogError("[MonkeyAnimatorBuilder] Sahnede 'PlayerMotor' bileşenine sahip bir Player objesi bulunamadı!");
            return;
        }

        CharacterController cc = motor.GetComponent<CharacterController>();
        if (cc == null)
            return;

        PlayerVisual playerVisual = motor.GetComponentInChildren<PlayerVisual>();
        if (playerVisual != null)
        {
            playerVisual.ResolveReferences();
            playerVisual.CacheModelRenderers();
        }

        Transform visualRoot = playerVisual != null ? playerVisual.VisualRoot : null;
        if (visualRoot == null || visualRoot == motor.transform)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        Bounds combinedWorldBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.transform == motor.transform)
                continue;

            if (!hasBounds)
            {
                combinedWorldBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                combinedWorldBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            return;

        float modelHeight = Mathf.Max(0.8f, combinedWorldBounds.size.y);
        float horizontalExtent = Mathf.Max(combinedWorldBounds.extents.x, combinedWorldBounds.extents.z);
        float recommendedRadius = Mathf.Clamp(horizontalExtent * 0.65f, 0.22f, modelHeight * 0.45f);
        float recommendedHeight = Mathf.Max(recommendedRadius * 2.1f, modelHeight);
        Vector3 recommendedCenter = new Vector3(0f, recommendedHeight * 0.5f, 0f);

        float modelBottomWorldY = combinedWorldBounds.min.y;
        float pivotOffsetFromFeet = visualRoot.position.y - modelBottomWorldY;

        Undo.RecordObject(cc, "Fit Character Controller to Monkey_B1");
        cc.height = recommendedHeight;
        cc.radius = recommendedRadius;
        cc.center = recommendedCenter;
        EditorUtility.SetDirty(cc);

        if (playerVisual != null)
        {
            Undo.RecordObject(playerVisual, "Align Monkey_B1 VisualOffset");
            playerVisual.VisualOffset = new Vector3(0f, pivotOffsetFromFeet, 0f);
            EditorUtility.SetDirty(playerVisual);
        }

        Debug.Log(
            $"<b>[MonkeyAnimatorBuilder] CharacterController 'Monkey_B1' modeline göre ayarlandı:</b> " +
            $"Height={cc.height:F2}m, Radius={cc.radius:F2}m, Eye Mesh Bulundu={(playerVisual != null && playerVisual.HasEyeMesh)}"
        );
    }

    private static string FindMonkeyFbxPath()
    {
        if (File.Exists(MonkeyB1FbxPath))
            return MonkeyB1FbxPath;

        string[] b1Guids = AssetDatabase.FindAssets("Monkey_B1 t:Model");
        if (b1Guids != null && b1Guids.Length > 0)
            return AssetDatabase.GUIDToAssetPath(b1Guids[0]);

        if (File.Exists(FallbackOldFbxPath))
            return FallbackOldFbxPath;

        string[] guids = AssetDatabase.FindAssets("maymun t:Model");
        if (guids != null && guids.Length > 0)
            return AssetDatabase.GUIDToAssetPath(guids[0]);

        return null;
    }

    private static AnimationClip MatchExactOrContains(
        List<AnimationClip> clips,
        HashSet<AnimationClip> usedClips,
        string[] exactNames,
        params string[] containsKeywords)
    {
        // 1. Önce tam isim eşleşmesine bak (örn. "Jump", "Jump2", "ClimbIdle", "ClimbSit", "RunR")
        if (exactNames != null)
        {
            for (int e = 0; e < exactNames.Length; e++)
            {
                string targetExact = exactNames[e].ToLowerInvariant();
                for (int i = 0; i < clips.Count; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null) continue;

                    string shortLower = GetShortClipName(clip).ToLowerInvariant();
                    if (shortLower == targetExact)
                    {
                        usedClips.Add(clip);
                        return clip;
                    }
                }
            }
        }

        // 2. Tam eşleşme yoksa alt-string anahtar kelimesine bak
        if (containsKeywords != null)
        {
            for (int k = 0; k < containsKeywords.Length; k++)
            {
                string kw = containsKeywords[k].ToLowerInvariant();
                for (int i = 0; i < clips.Count; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null) continue;

                    if (clip.name.ToLowerInvariant().Contains(kw))
                    {
                        usedClips.Add(clip);
                        return clip;
                    }
                }
            }
        }

        return null;
    }

    private static AnimationClip ResolveWithFallback(
        string stateName,
        AnimationClip directClip,
        List<string> fallbackStateList,
        params AnimationClip[] fallbacks)
    {
        if (directClip != null)
            return directClip;

        fallbackStateList.Add(stateName);
        for (int i = 0; i < fallbacks.Length; i++)
        {
            if (fallbacks[i] != null)
                return fallbacks[i];
        }
        return null;
    }

    private static AnimatorState CreateRunBlendTreeState(
        AnimatorController controller,
        AnimatorStateMachine sm,
        AnimationClip walkClip,
        AnimationClip runClip,
        AnimationClip sprintClip,
        Vector3 position)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Run", out BlendTree blendTree, 0);
        state.motion = blendTree;

        ChildAnimatorState[] states = sm.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == state)
            {
                states[i].position = position;
                break;
            }
        }
        sm.states = states;

        blendTree.name = "RunBlendTree";
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = MonkeyAnimationDriver.ParamSpeed;
        blendTree.useAutomaticThresholds = false;

        if (walkClip != null)
            blendTree.AddChild(walkClip, 0f);
        if (runClip != null)
            blendTree.AddChild(runClip, 7f);
        if (sprintClip != null)
            blendTree.AddChild(sprintClip, 12f);

        return state;
    }

    private static void ConfigureTransitions(
        AnimatorStateMachine sm,
        AnimatorState stateIdle,
        AnimatorState stateRun,
        AnimatorState stateJumpUp,
        AnimatorState stateFall,
        AnimatorState stateLand,
        AnimatorState stateSlide,
        AnimatorState stateWallLeft,
        AnimatorState stateWallRight,
        AnimatorState stateHang,
        AnimatorState stateSwing,
        AnimatorState stateTransfer)
    {
        AnimatorStateTransition idleToRun = CreateFastTransition(stateIdle, stateRun, 0.10f);
        idleToRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition runToIdle = CreateFastTransition(stateRun, stateIdle, 0.12f);
        runToIdle.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.20f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition anyToJump = CreateFastAnyTransition(sm, stateJumpUp, 0.10f);
        anyToJump.AddCondition(AnimatorConditionMode.If, 0f, MonkeyAnimationDriver.ParamJumpTrigger);

        AnimatorStateTransition jumpToFall = CreateFastTransition(stateJumpUp, stateFall, 0.14f);
        jumpToFall.AddCondition(AnimatorConditionMode.Less, -0.5f, MonkeyAnimationDriver.ParamVerticalVelocity);

        AnimatorStateTransition idleToFall = CreateFastTransition(stateIdle, stateFall, 0.12f);
        idleToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);
        idleToFall.AddCondition(AnimatorConditionMode.Less, -1.0f, MonkeyAnimationDriver.ParamVerticalVelocity);

        AnimatorStateTransition runToFall = CreateFastTransition(stateRun, stateFall, 0.12f);
        runToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);
        runToFall.AddCondition(AnimatorConditionMode.Less, -1.0f, MonkeyAnimationDriver.ParamVerticalVelocity);

        AnimatorStateTransition fallToLand = CreateFastTransition(stateFall, stateLand, 0.10f);
        fallToLand.AddCondition(AnimatorConditionMode.If, 0f, MonkeyAnimationDriver.ParamLandTrigger);

        AnimatorStateTransition fallToGroundedRun = CreateFastTransition(stateFall, stateRun, 0.12f);
        fallToGroundedRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        fallToGroundedRun.AddCondition(AnimatorConditionMode.Greater, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition fallToGroundedIdle = CreateFastTransition(stateFall, stateIdle, 0.12f);
        fallToGroundedIdle.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        fallToGroundedIdle.AddCondition(AnimatorConditionMode.Less, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition jumpToGroundedRun = CreateFastTransition(stateJumpUp, stateRun, 0.12f);
        jumpToGroundedRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        jumpToGroundedRun.AddCondition(AnimatorConditionMode.Greater, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition jumpToGroundedIdle = CreateFastTransition(stateJumpUp, stateIdle, 0.12f);
        jumpToGroundedIdle.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        jumpToGroundedIdle.AddCondition(AnimatorConditionMode.Less, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition landToRun = CreateFastTransition(stateLand, stateRun, 0.12f);
        landToRun.AddCondition(AnimatorConditionMode.Greater, 0.35f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition landToIdle = CreateFastTransition(stateLand, stateIdle, 0.14f);
        landToIdle.AddCondition(AnimatorConditionMode.Less, 0.35f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition anyToSlide = CreateFastAnyTransition(sm, stateSlide, 0.10f);
        anyToSlide.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Sliding, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition slideToRun = CreateFastTransition(stateSlide, stateRun, 0.12f);
        slideToRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        slideToRun.AddCondition(AnimatorConditionMode.Greater, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition slideToIdle = CreateFastTransition(stateSlide, stateIdle, 0.12f);
        slideToIdle.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);
        slideToIdle.AddCondition(AnimatorConditionMode.Less, 0.25f, MonkeyAnimationDriver.ParamSpeed);

        AnimatorStateTransition slideToFall = CreateFastTransition(stateSlide, stateFall, 0.12f);
        slideToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition anyToWallLeft = CreateFastAnyTransition(sm, stateWallLeft, 0.11f);
        anyToWallLeft.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.WallRunning, MonkeyAnimationDriver.ParamState);
        anyToWallLeft.AddCondition(AnimatorConditionMode.Less, 0f, MonkeyAnimationDriver.ParamWallSide);

        AnimatorStateTransition anyToWallRight = CreateFastAnyTransition(sm, stateWallRight, 0.11f);
        anyToWallRight.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.WallRunning, MonkeyAnimationDriver.ParamState);
        anyToWallRight.AddCondition(AnimatorConditionMode.Greater, 0f, MonkeyAnimationDriver.ParamWallSide);

        AnimatorStateTransition wallLeftToFall = CreateFastTransition(stateWallLeft, stateFall, 0.12f);
        wallLeftToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition wallRightToFall = CreateFastTransition(stateWallRight, stateFall, 0.12f);
        wallRightToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition wallLeftToRun = CreateFastTransition(stateWallLeft, stateRun, 0.12f);
        wallLeftToRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition wallRightToRun = CreateFastTransition(stateWallRight, stateRun, 0.12f);
        wallRightToRun.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Grounded, MonkeyAnimationDriver.ParamState);

        // Dala Tutunma (Hang -> ClimbIdle) ve Dalda Sallanma (Swing -> ClimbSit)
        AnimatorStateTransition anyToHang = CreateFastAnyTransition(sm, stateHang, 0.12f);
        anyToHang.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Hanging, MonkeyAnimationDriver.ParamState);
        anyToHang.AddCondition(AnimatorConditionMode.Less, 0.45f, MonkeyAnimationDriver.ParamSwingVelocity);

        AnimatorStateTransition anyToSwing = CreateFastAnyTransition(sm, stateSwing, 0.12f);
        anyToSwing.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Hanging, MonkeyAnimationDriver.ParamState);
        anyToSwing.AddCondition(AnimatorConditionMode.Greater, 0.45f, MonkeyAnimationDriver.ParamSwingVelocity);

        AnimatorStateTransition hangToSwing = CreateFastTransition(stateHang, stateSwing, 0.14f);
        hangToSwing.AddCondition(AnimatorConditionMode.Greater, 0.45f, MonkeyAnimationDriver.ParamSwingVelocity);

        AnimatorStateTransition swingToHang = CreateFastTransition(stateSwing, stateHang, 0.15f);
        swingToHang.AddCondition(AnimatorConditionMode.Less, 0.35f, MonkeyAnimationDriver.ParamSwingVelocity);

        AnimatorStateTransition hangToFall = CreateFastTransition(stateHang, stateFall, 0.12f);
        hangToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition swingToFall = CreateFastTransition(stateSwing, stateFall, 0.12f);
        swingToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);

        // Daldan Dala Geçiş (Transfer -> SitClimb)
        AnimatorStateTransition anyToTransfer = CreateFastAnyTransition(sm, stateTransfer, 0.10f);
        anyToTransfer.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Transferring, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition transferToHang = CreateFastTransition(stateTransfer, stateHang, 0.12f);
        transferToHang.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Hanging, MonkeyAnimationDriver.ParamState);

        AnimatorStateTransition transferToFall = CreateFastTransition(stateTransfer, stateFall, 0.12f);
        transferToFall.AddCondition(AnimatorConditionMode.Equals, (int)PlayerMotor.PlayerState.Airborne, MonkeyAnimationDriver.ParamState);
    }

    private static AnimatorStateTransition CreateFastTransition(AnimatorState from, AnimatorState to, float duration = FastTransitionDuration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.Destination;
        return transition;
    }

    private static AnimatorStateTransition CreateFastAnyTransition(AnimatorStateMachine sm, AnimatorState to, float duration = FastTransitionDuration)
    {
        AnimatorStateTransition transition = sm.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        return transition;
    }

    private static string GetShortClipName(AnimationClip clip)
    {
        if (clip == null || string.IsNullOrEmpty(clip.name))
            return "Yok";

        int lastPipe = clip.name.LastIndexOf('|');
        return (lastPipe >= 0 && lastPipe < clip.name.Length - 1)
            ? clip.name.Substring(lastPipe + 1)
            : clip.name;
    }

    private static void AssignControllerToScenePlayer(AnimatorController controller, string unmatchedSummary, bool verboseLog)
    {
        PlayerMotor motor = UnityEngine.Object.FindObjectOfType<PlayerMotor>();
        if (motor == null)
            return;

        string fbxPath = FindMonkeyFbxPath();
        Avatar fbxAvatar = !string.IsNullOrEmpty(fbxPath) ? AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath) : null;

        Animator childModelAnimator = null;
        SkinnedMeshRenderer smr = motor.GetComponentInChildren<SkinnedMeshRenderer>(false);
        if (smr != null)
        {
            childModelAnimator = smr.GetComponentInParent<Animator>();
            if (childModelAnimator != null && childModelAnimator.transform == motor.transform)
            {
                childModelAnimator = null;
            }
        }

        if (childModelAnimator == null)
        {
            Animator[] anims = motor.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i].transform != motor.transform)
                {
                    childModelAnimator = anims[i];
                    break;
                }
            }
        }

        Animator rootAnimator = motor.GetComponent<Animator>();
        if (rootAnimator != null && childModelAnimator != null && rootAnimator != childModelAnimator)
        {
            rootAnimator.enabled = false;
        }

        if (childModelAnimator != null)
        {
            bool changed = false;
            if (childModelAnimator.runtimeAnimatorController != controller)
            {
                childModelAnimator.runtimeAnimatorController = controller;
                changed = true;
            }
            if (fbxAvatar != null && childModelAnimator.avatar != fbxAvatar)
            {
                childModelAnimator.avatar = fbxAvatar;
                changed = true;
            }
            if (childModelAnimator.applyRootMotion)
            {
                childModelAnimator.applyRootMotion = false;
                changed = true;
            }
            if (childModelAnimator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                childModelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(childModelAnimator);
                if (verboseLog)
                {
                    Debug.Log($"<b>[MonkeyAnimatorBuilder]</b> '{childModelAnimator.gameObject.name}' üzerindeki Animator'a MonkeyController otomatik bağlandı!");
                }
            }
        }

        PlayerVisual visual = motor.GetComponentInChildren<PlayerVisual>(true);
        if (visual != null)
        {
            visual.ResolveReferences();
            visual.CacheModelRenderers();
            EditorUtility.SetDirty(visual);
        }

        MonkeyAnimationDriver driver = motor.GetComponentInChildren<MonkeyAnimationDriver>(true);
        if (driver != null)
        {
            if (childModelAnimator != null && driver.TargetAnimator != childModelAnimator)
            {
                driver.TargetAnimator = childModelAnimator;
                EditorUtility.SetDirty(driver);
            }
            if (!string.IsNullOrEmpty(unmatchedSummary))
            {
                driver.UnmatchedClipsReport = unmatchedSummary;
                EditorUtility.SetDirty(driver);
            }
        }
    }

    private static void EnsureDirectoryExists(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
#endif
