#if UNITY_EDITOR
namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// Unity メニューバー <c>Peak Plunder</c> 配下のパス定数。
    /// エディタツール追加時はここに定義してから各 MenuItem で参照する。
    /// </summary>
    public static class PeakPlunderEditorMenus
    {
        public const string Root = "Peak Plunder";

        public static class Stage01
        {
            public const string BuildGameplayScene = Root + "/Stage01/Build Gameplay Scene";
            public const string ValidateGameplayScene = Root + "/Stage01/Validate Gameplay Scene";
            public const string PlaceGrappableRocks = Root + "/Stage01/Place Grappable Rocks";
        }

        public static class Terrain
        {
            public const string GenerateMountainTerrain = Root + "/Terrain/Generate Mountain Terrain";
        }

        public static class Items
        {
            public const string GenerateItemDefinitions = Root + "/Items/Generate Item Definition Assets";
            public const string WireItemGameplaySystems = Root + "/Items/Wire Item Gameplay Systems";
            public const string WirePlayerPrefabOnly = Root + "/Items/Wire Player Prefab Only";
        }

        public static class Network
        {
            public const string BuildRuntimeItemPrefabs = Root + "/Network/Build Runtime Item Prefabs";
        }

        public static class Bootstrap
        {
            public const string BootstrapAllAssets = Root + "/Bootstrap/Bootstrap All Assets";
            public const string CreateAudioMixer = Root + "/Bootstrap/Create Audio Mixer";
            public const string PopulateSoundLibrary = Root + "/Bootstrap/Populate Sound Library";
            public const string BootstrapLocalization = Root + "/Bootstrap/Bootstrap Localization";
            public const string CreateRigTemplate = Root + "/Bootstrap/Create Rig Template";
        }

        public static class Offline
        {
            public const string CreateOfflineTestScene = Root + "/Offline/Create Offline Test Scene";
            public const string RefreshOfflineTestScene = Root + "/Offline/Refresh Offline Test Scene";
            public const string LoopImplementOfflineTestScene = Root + "/Offline/Loop Implement OfflineTestScene";
            public const string CreateCombinedScene = Root + "/Offline/Create Combined Scene";

            public static class Combined
            {
                public const string SetupLocalCoop = Root + "/Offline/Combined/Setup Local Co-op";
                public const string SetupOnline = Root + "/Offline/Combined/Setup Online";
                public const string SetupWireRope = Root + "/Offline/Combined/Setup Wire Rope";
                public const string VerifyWireRope = Root + "/Offline/Combined/Verify Wire Rope";
            }
        }

        public static class Build
        {
            public const string BuildMacOS = Root + "/Build/Build macOS";
            public const string BuildWindows = Root + "/Build/Build Windows x64";
        }
    }
}
#endif
