using PeakPlunder.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sandbox.UI
{
    /// <summary>
    /// タイトル / ショップシーン向けの共通ブートストラップ。
    /// AudioManager・SaveManager が無い軽量シーンでも BGM / UI SE / チュートリアル保存が動く。
    /// </summary>
    public sealed class MenuSceneBootstrap : MonoBehaviour
    {
        public enum MenuSceneKind { Title, Shop }

        [SerializeField] private MenuSceneKind _kind = MenuSceneKind.Title;

        private void Awake()
        {
            EnsureAudioManager();
            EnsureSaveManager();
        }

        private void Start()
        {
            PlaySceneBgm();
        }

        private void EnsureAudioManager()
        {
            if (GameServices.Audio != null) return;

            var go = new GameObject("[MenuAudioManager]");
            go.AddComponent<AudioManager>();
            DontDestroyOnLoad(go);
        }

        private void EnsureSaveManager()
        {
            if (GameServices.Save != null) return;

            var go = new GameObject("[MenuSaveManager]");
            go.AddComponent<SaveManager>();
            DontDestroyOnLoad(go);
        }

        private void PlaySceneBgm()
        {
            var audio = GameServices.Audio;
            if (audio == null) return;

            var clip = _kind == MenuSceneKind.Shop
                ? MenuAmbientBgmFactory.GetShopBgm()
                : MenuAmbientBgmFactory.GetTitleBgm();
            audio.PlayBGM(clip, _kind == MenuSceneKind.Shop ? 0.28f : 0.32f);
        }

        /// <summary>シーン名から種別を推定して Bootstrap を生成する。</summary>
        public static void EnsureForActiveScene(Transform parent)
        {
            if (Object.FindFirstObjectByType<MenuSceneBootstrap>() != null) return;

            string scene = SceneManager.GetActiveScene().name;
            var kind = scene switch
            {
                var s when s == GameFlow.ShopScene  => MenuSceneKind.Shop,
                var s when s == GameFlow.TitleScene => MenuSceneKind.Title,
                var s when s == "MainMenu"          => MenuSceneKind.Title,
                _ => (MenuSceneKind?)null
            };
            if (kind == null) return;

            var go = new GameObject("MenuSceneBootstrap");
            go.transform.SetParent(parent, false);
            var bootstrap = go.AddComponent<MenuSceneBootstrap>();
            bootstrap.Configure(kind.Value);
        }

        public void Configure(MenuSceneKind kind) => _kind = kind;
    }
}
