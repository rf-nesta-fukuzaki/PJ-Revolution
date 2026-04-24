#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// GDD §16 — Animation Rigging セットアップのテンプレート生成。
    ///
    /// 目的:
    ///   キャラクターリグ FBX 未着手のため、まず Rigging 構造のテンプレートを作成しておく。
    ///   実際のキャラクターモデル (ExplorerRig.fbx) を Assets にインポートしたあと、
    ///   本テンプレートをドラッグして Source/Target 参照を差し込むだけで済む状態にする。
    ///
    /// 構成:
    ///   PlayerRigTemplate (root)
    ///     └ Animator
    ///     └ RigBuilder
    ///         └ Rig (GameObject + Rig component)
    ///             ├ LeftHandIK  (TwoBoneIKConstraint)  — クライミング／遺物保持用
    ///             ├ RightHandIK (TwoBoneIKConstraint)  — クライミング／遺物保持用
    ///             ├ LeftFootIK  (TwoBoneIKConstraint)  — 斜面の足位置補正
    ///             └ RightFootIK (TwoBoneIKConstraint)  — 斜面の足位置補正
    ///
    /// 起動: Tools > PeakPlunder > Create Rig Template
    /// </summary>
    public static class PeakPlunderRiggingBootstrap
    {
        private const string PREFAB_DIR  = "Assets/Sandbox/Prefabs";
        private const string PREFAB_PATH = "Assets/Sandbox/Prefabs/PlayerRigTemplate.prefab";

        [MenuItem("Tools/PeakPlunder/Create Rig Template")]
        public static void CreateRigTemplate()
        {
            if (!Directory.Exists(PREFAB_DIR))
                Directory.CreateDirectory(PREFAB_DIR);

            if (File.Exists(PREFAB_PATH))
            {
                Debug.Log($"[PeakPlunder] Rig template already exists at {PREFAB_PATH}");
                return;
            }

            var root = new GameObject("PlayerRigTemplate");
            root.AddComponent<Animator>();
            var rigBuilder = root.AddComponent<RigBuilder>();

            var rigRoot = new GameObject("Rig");
            rigRoot.transform.SetParent(root.transform, false);
            var rig = rigRoot.AddComponent<Rig>();

            AddTwoBoneIK(rigRoot, "LeftHandIK");
            AddTwoBoneIK(rigRoot, "RightHandIK");
            AddTwoBoneIK(rigRoot, "LeftFootIK");
            AddTwoBoneIK(rigRoot, "RightFootIK");

            rigBuilder.layers.Add(new RigLayer(rig, true));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PeakPlunder] Rig template created at {PREFAB_PATH}: {(prefab != null ? prefab.name : "null")}");
        }

        private static void AddTwoBoneIK(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ik = go.AddComponent<TwoBoneIKConstraint>();
            ik.weight = 1f;
            // data は Inspector から差し込む想定（Root/Mid/Tip/Target/Hint）
        }
    }
}
#endif
