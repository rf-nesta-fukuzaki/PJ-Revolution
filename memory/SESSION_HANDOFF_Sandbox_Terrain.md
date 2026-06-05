# セッション引き継ぎ書 — Sandbox 地形生成（手続き的・Chunkベース）

ステータス: **Module 3 (Biome / Placement / 浸食時分割) 完了・検証済み（未コミット）**
更新日: 2026-05-29
引き継ぎ元セッション: `bf919a00` / `df754bdd`（引き継ぎ失敗）
※ 過去の中断理由: Claude Code の thinking ブロック不具合による API 400（コードの問題ではない）

---

## これは何の作業か

`Assets/Sandbox/Scene/Sandbox.unity` 向けの**手続き的地形生成システム**。
URP 固定 / C# + HLSL Compute Shader。Chunk ベースのライフサイクル管理 +
RidgedMultifractal による高さ場生成 + GPU 水理/熱浸食（Hydraulic/Thermal Erosion）。

スクリプトは全て `Assets/Sandbox/Script/World/` 配下（CLAUDE.md の指示通り Sandbox 直下）。

> ⚠️ **重要**: これらのファイルはディスク上に存在するが **git 未追跡（未コミット）**。
> 続行前にまずコミットして作業をロックすることを強く推奨。

---

## 実装済みファイル（ディスク上に存在・未コミット）

### Chunk 基盤
- `World/ChunkCoord.cs` — チャンク座標
- `World/ChunkContext.cs` — チャンク生成コンテキスト
- `World/ChunkHandle.cs` — チャンクハンドル
- `World/ChunkBufferSet.cs` — GPU バッファ群（`HeightFixed` バッファ追加済み）
- `World/ChunkManager.cs` — チャンク状態機械（浸食パスを統合済み）
- `World/IChunkLifecyclePolicy.cs` / `World/DistanceBasedLifecyclePolicy.cs` — 距離ベース生成/破棄

### 高さ場生成
- `World/IHeightfieldBuilder.cs` — 高さ場ビルダー interface
- `World/Generation/Base/RidgedMultifractalBuilder.cs`
- `World/Generation/Base/Shaders/NoisePrimitives.hlsl`
- `World/Generation/Base/Shaders/RidgedMFKernel.compute`
- `World/Config/RidgedMFParams.cs` + `Sandbox/Data/RidgedMFParams.asset`
- `World/TerrainGenerator.cs` — 浸食設定を含む
- `World/TerrainDebugMeshBaker.cs` — Ready 状態でメッシュをベイク

### Module 2: Erosion（浸食）
- `World/IErosionPass.cs` — 浸食パス interface（+ `IErosionJob` time-slicing 追加）
- `World/Generation/Erosion/HydraulicThermalErosionGPU.cs` — C# ラッパー（CreateJob で時分割対応）
- `World/Generation/Erosion/Shaders/HydraulicErosion.compute` — Pack/Droplet/Thermal/Unpack/Normal カーネル
- `World/Config/ErosionParams.cs` + `Sandbox/Data/ErosionParams.asset`

### Module 3: Biome / Placement / 時分割（2026-05-29 追加・検証済み）
- `World/Generation/Placement/PlacementInstance.cs` — Append 用インスタンス型（stride 24, HLSL と一致）
- `World/IBiomePass.cs` / `World/Generation/Biome/BiomeClassifierGPU.cs` / `.../Shaders/BiomeClassify.compute`
- `World/Config/BiomeParams.cs` + `Sandbox/Data/BiomeParams.asset`（+ `BiomeId` enum: Water/Sand/Grass/Forest/Rock/Snow）
- `World/IPlacementPass.cs` / `World/Generation/Placement/ScatterPlacementGPU.cs` / `.../Shaders/PlacementScatter.compute`（prototype 別に Tree/Rock の 2 Append バッファへ出力）
- `World/Config/PlacementParams.cs` + `Sandbox/Data/PlacementParams.asset`
- `World/PlacementIndirectRenderer.cs` + `.../Shaders/PlacementInstancedIndirect.shader` — CPU 読戻し無しの DrawMeshInstancedIndirect 描画（CopyCount→args、procedural instancing で StructuredBuffer から TRS。Cylinder=木 / Cube=岩、material 未指定時はシェーダーからフォールバック生成）
- 変更: `ChunkBufferSet`(BiomeMaskTex + Tree/Rock 2 Append バッファ)、`ChunkManager`(Base→Erosion→Biome→Placement→Ready の予算制状態機械)、`TerrainGenerator`(各 shader/params + 予算/配置/material フィールド)

### Module 3 改良（2026-05-29 追加・検証済み）
- **①Placement を indirect instancing 化**: 旧 `PlacementDebugRenderer`(CPU readback + DrawMeshInstanced) を削除し、`PlacementIndirectRenderer`(GPU 直描画) に置換。ChunkBufferSet は prototype 別 2 Append バッファ、renderer がチャンク毎に IndirectArgs を持ち CopyCount で instanceCount を流し込む。シェーダーは `Sandbox/PlacementInstancedIndirect`（procedural:setup, unlit フラット着色）。
- **②BiomeParams チューニング**: 地形レンジに合わせ beach=2/grass=16/forest=50/snow=70/rockSlope=35。さらに `BiomeClassify.compute` の分類順で **Snow を Rock より先に**判定（山頂の雪冠が出るように）。結果 Water10/Sand33/Grass30/Forest3/Rock22/Snow3%。
- **③浸食 time-slice 細粒度化**: `ErosionParams.dropletsPerBatch`(=2000) を追加し固定 8000 を置換。erosion op 数 14→32 でフレームスパイク低減。

### Module 3 改良 第2弾（2026-05-29 追加・検証済み）
- **インスタンスのライティング**: `PlacementInstancedIndirect.shader` に法線 + URP main light ランバート + SH 環境光を追加（unlit → 簡易ライティング）。
- **実メッシュ/マテリアルのアセット化**: `Sandbox/Data/` に `PlacementTreeMesh.asset`(cone)・`PlacementRockMesh.asset`(icosahedron)・`PlacementTreeMat.mat`(緑)・`PlacementRockMat.mat`(灰) を生成し、シーンの `TerrainGenerator` に割当・保存。未割当時は標準 Primitive + シェーダーフォールバックで動作。
- **Biome 重みブレンド**: `BiomeClassify.compute` を 6 バイオームの smoothstep 重み計算に拡張。`BiomeColorTex`(ARGBHalf, ChunkBufferSet 追加) にブレンド色を出力、`BiomeMaskTex` は argmax index。`BiomeParams` に `altitudeBlend`/`slopeBlend` 追加。検証: ブレンド遷移セル 75%・NaN=0。

### Module 3 改良 第3弾（2026-05-29 追加・検証済み）
- **シャドウ対応**: `PlacementInstancedIndirect.shader` / `TerrainBiomeSampled.shader` に **ShadowCaster pass** 追加 + Forward pass で main light シャドウ受け（`GetMainLight(shadowCoord)`/`shadowAttenuation`）。両シェーダー passCount=2・supported 確認。配置インスタンスは procedural:setup を ShadowCaster でも実行し正しい位置で投影。
- **地形 UV サンプルシェーダー化**: 頂点色方式を廃し、`Sandbox/TerrainBiomeSampled.shader` が `BiomeColorTex` を UV サンプル（per-pixel 着色、頂点密度非依存）。`TerrainDebugMeshBaker` は MaterialPropertyBlock で per-chunk に BiomeColorTex + apron 除外 UV リマップ(`_UVScale`/`_UVOffset`)をバインド。BiomeColorTex の readback は廃止（HeightTex のみ）。旧 `TerrainVertexColor.shader` 削除。
- **LOD/カリング**: MeshBaker が `ctx.Lod` に応じ頂点を間引き（step=1<<lod → LOD0/1/2 で 129/65/33 頂点/辺、N=129）。`PlacementIndirectRenderer` に Camera.main フラスタムでのチャンク単位カリング（`GeometryUtility.TestPlanesAABB`、`LastDrawnChunks`/`LastCulledChunks` 公開）。地形メッシュは MeshRenderer なので Unity 標準カリング。
- **実アート差し替え**: コードでなく運用。`TerrainGenerator` の placementTree/RockMesh・Material と debugMeshMaterial にモデル/マテリアルを割り当てれば差し替わる（現状は cone/ico + 自前 .mat）。

### PEAK 級改修 Step 8（2026-05-29 追加・検証済み）
A: ゲームフィール仕上げ / B: プロシージャルアート向上 / C: ビルド準備。

**A 新規/変更**:
- `SandboxFootstepAudio.cs`（新規・プレイヤー root に付与）: 接地+水平速度で足音、空中→接地で着地音、専用 AudioSource にプロシージャル wind ループを高度+速度連動音量で再生。
- `SandboxCameraShake.cs`（変更）: 落下死バースト（プレイヤー Y < fallDeathShakeY=-15 で trauma=1、地上復帰で再武装）追加。
- `SandboxGameplayDirector.cs`（変更）: EventSystem + `InputSystemUIInputModule` 生成（RETRY ボタン有効化）、footstep audio 付与、SummitPanel active 監視で祝祭シェイク。

**B 変更**:
- `SandboxGrappableHints.cs`: Cube → プロシージャルボルダー（緯度経度球を radial ノイズ変形）+ MeshCollider(convex, grapple raycast 用)。`rockPrefab` 割当で実アート差し替え可。
- `SandboxSummitGoal.cs`: 山頂にポール+両面旗の装飾（コライダー無し・トリガー球の子）。

**C 新規**:
- `Assets/Sandbox/Script/UI/SandboxStartMenu.cs`（UGS 非依存の単機メニュー。PLAY→Sandbox / QUIT。UI runtime 生成 + EventSystem）。
- `Assets/Sandbox/Scene/StartMenu.unity`（新規シーン。MenuCamera + StartMenu）。
- `Assets/Sandbox/Editor/Build/SandboxBuild.cs`（メニュー Peak Plunder/Build/Build macOS・Build Windows x64）。
- `README.md`（操作/ゴール/シーン構成/ビルド方法）。
- **Build Settings**: [0]StartMenu [1]Sandbox を enabled、他は無効で温存。

**C その他の変更（ビルド阻害の修正）**:
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`: ForceReserialize で missing types（URPTerrainShaderSetting 等）を解消 + `m_AssetVersion: 10→9`。**原因: URP パッケージがダウングレード（manifest が `M`）され `k_LastVersion=9` に対しアセットが 10 で「not at last version」だった**。migration は upgrade のみ対応のため手動で版を合わせた（データは v9 スキーマで再シリアライズ済み）。

**検証**:
- コンパイル 0 エラー。Sandbox Play: FootstepAudio/CameraShake/EventSystem OK、ボルダー 5/5 に MeshCollider・tag=Grappable、山頂旗 pole+cloth、Console Error 0。URP アセット変更後も Play エラー 0（レンダリング健全）。
- StartMenu Play: Canvas 6要素、PLAY interactable、Sandbox loadable=True。
- **dry build（macOS dev, Mono2x）**: URP 版修正で preprocess 突破 → 98MB まで到達 → 最終 **UnityLinker（managed stripping）段で Fatal**。

**Step 8 で残した暫定 / 次タスク**:
- **ビルド未完（既存・プロジェクト全体問題）**: `Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly 'nunit.framework'`。原因は `*.DocCodeExamples.dll` / `glTFast.Documentation.Examples.dll` などパッケージのサンプル/ドキュメント用アセンブリが player ビルドに混入し nunit を参照する点。Step 8 コードとは無関係。修正案: これら example/test アセンブリを player から除外（パッケージ埋め込み+asmdef 修正、または build callback で除去）。ManagedStrippingLevel は元から Disabled。
- RETRY ボタンは EventSystem 生成済みで機能可能（クリックでシーンリロード）。
- カメラシェイクは位置のみ（回転無し）。
- 風音/足音は手続き生成プレースホルダ（実 SE 差し替え余地）。

### PEAK 級改修 Step 7（2026-05-29 追加・検証済み）
単機プレイ完成度向上。既存の Mountain01 用ゲームループ（Timer/HUD/Summit/Checkpoint/Respawn/Audio/Fade）を Sandbox に接続 + カメラシェイク。

**新規ファイル**:
- `Assets/Sandbox/Script/World/Integration/SandboxGameplayDirector.cs` — プレイヤーリグ昇格（RopeSystem 取付）完了後に、未生成なら `IrisTransition`(自己生成) / `AudioManager` / `TimerDisplay`(操作開始時に Restart) / `HudManager` を生成。`CheckpointSystem` は CheckpointBaker が自前生成するため対象外。既存コードは未変更（接着のみ）。
- `Assets/Sandbox/Script/World/Integration/SandboxCameraShake.cs` — `[DefaultExecutionOrder(200)]`。FirstPersonLook(0) が毎フレーム base に再設定する `cameraRig.localPosition` に LateUpdate で**加算**（非累積・位置のみ。回転は FPL と競合のため不使用）。トリガーは公開プロパティのポーリングで自己検出: 着地（`PlayerMovement.IsGrounded` false→true・直前落下速度比例）/ ロープ接続（`RopeSystem.IsAttached` false→true）。外部は `AddTrauma(amount)`。

**変更**:
- `Assets/Scripts/World/CheckpointSystem.cs` — **バグ修正（非破壊・両シーン共通の堅牢化）**: `Start()` でプレイヤー未生成（非同期スポーン）だと `_playerTransform=null` のまま `Update` が永久 early-return し落下リスポーンが死ぬ問題を、`TryAcquirePlayer()` を切り出し `Update` で null の間は毎フレーム遅延取得するよう修正。初回取得位置を `_defaultRespawnPosition`（基準=スポーン地点）に。
- `SandboxBootstrap.cs` — `autoAttachSpawners` に `SandboxGameplayDirector` を追加。

**検証（Sandbox.unity Play + RunCommand 機能テスト）**:
- シングルトン全生成 ✓: TimerDisplay(計測中) / HudManager(HUD_Canvas 子 7) / AudioManager / CheckpointSystem(total=3) / IrisTransition / CameraShake(カメラに付与)
- **山頂到達フロー** ✓: プレイヤーを SummitGoal へテレポート → Timer 凍結(23.61) + SummitPanel active + 表示 "SUMMIT REACHED! | 00:23.61 | Best: 00:04.62" + BestTime PlayerPrefs 保存
- **落下リスポーン** ✓: 修正前は Y=-1548 へ落下継続（_playerTransform null バグ）→ 修正後 Y=-50 へ落下 → スポーン地点 (130.6,6.6,127.2) に復帰、fallDeathY 上に正しく戻る
- Console Error 0 件

**Step 7 で残した暫定**:
- SummitPanel の RETRY ボタンは EventSystem 未配置のため非インタラクティブ（新 Input System の UI モジュール配線が必要）。表示・タイム・Best 保存は機能。再挑戦インタラクションは別途。
- カメラシェイクは位置のみ（回転シェイク無し）。山頂到達/落下死の専用バースト（`AddTrauma` 呼び出し）は未配線（自己トリガーは着地・ロープ接続のみ）。
- 足音/風音の高度・バイオーム連動、山頂 trigger 連動のカメラリフトは未実装（候補のまま）。

### PEAK 級改修 Step 6（2026-05-29 追加・検証済み）
パフォーマンス計測 & 60fps 設定。Co-op は**ユーザー意向で後回し**（方式未確定）。

**新規ファイル**:
- `Assets/Sandbox/Script/World/Integration/SandboxPerformanceConfig.cs` — `[DefaultExecutionOrder(-40)]`。`Application.targetFrameRate=60`/`QualitySettings.vSyncCount=0`/`Time.maximumDeltaTime=0.1` を OnEnable で設定。地形のフレーム予算（TerrainGenerator 側）は非破壊で触らない。

**変更**:
- `SandboxBootstrap.cs` — `autoAttachSpawners` 先頭に `SandboxPerformanceConfig` を追加。

**検証（Sandbox.unity Play）**:
- PerformanceConfig attached ✓ / targetFrameRate=60 / vSync=0 / maximumDeltaTime=0.1 ✓
- FrameTimingManager 実測（エディタ+プロファイラ+MCP オーバーヘッド込み）: CPU avg 16.53ms / max 18.05ms、GPU avg 13.69ms / max 13.69→15.82ms。→ **CPU 律速・GPU 余裕**。シーン軽量（MeshRenderer 21 / ParticleSystem 1 / Active chunk 9）。ビルドでは余裕の見込み。
- Console Error 0 / Warning 1（URP global settings の既存 `URPTerrainShaderSetting` missing types。Step 6 と無関係）

**Co-op の状況（未着手・要方式決定）**:
- 入力は静的 `InputStateReader`（グローバル KB/Mouse）依存 → ローカル複数人は**入力層の作り直しが必須**（破壊的）。
- NGO 2.11 + Unity Transport 2.4.0 は導入済みだがシーン側未配線（破壊的）。
- 2026-05-29 ユーザー選択「Co-op は後回し」。次 Step は単機プレイ完成度向上に充てる方針。

### PEAK 級改修 Step 5（2026-05-29 追加・検証済み）
環境演出 & ビジュアル品質。時刻サイクル・プロシージャル空・URP Volume・雲海カメラ追従・山頂演出。

**新規ファイル**:
- `Assets/Sandbox/Script/World/Environment/DayNightCycle.cs` — `[RequireComponent(AtmosphericProfileController)]`。`TimeOfDay` を実時間で進行（`dayLengthSeconds=600`、loop/pingPong 切替）。
- `Assets/Sandbox/Script/World/Environment/ProceduralSky.cs` — `Sandbox/ProceduralGradientSky` を `RenderSettings.skybox` に差込（見つからなければ skip、Disable で復元）。`[DefaultExecutionOrder(-26)]`
- `Assets/Sandbox/Script/World/Environment/Shaders/ProceduralGradientSky.shader` — URP Skybox。上半球 horizon→zenith / 下半球 horizon→ground のグラデ + Sun disk。色/太陽は全て Shader Global 受信（`_SkyZenith/_SkyHorizon/_SkyGround/_SkySunDir/_SkySunColor/_SkySunSize`）。
- `Assets/Sandbox/Script/World/Environment/VolumeProfileSetup.cs` — URP Volume を runtime 生成。`Tonemapping(Neutral)/ColorAdjustments/Bloom/Vignette` を `VolumeProfile.Add<>` で追加。asset 不要。
- `Assets/Sandbox/Script/World/Environment/SummitVisualEffects.cs` — 山頂に光柱（自前コーンメッシュ `BuildConeMesh`）+ 紙吹雪 `ParticleSystem`。URP/Unlit を Transparent 化した共有マテリアル。

**変更**:
- `AtmosphericProfileController.cs` — Step 4 の 2 点補間を **5 キー時刻補間**（Dawn/Morning/Noon/Dusk/Night）に拡張。`public float TimeOfDay { get; set; }`（set で `ApplyTimeDependent()` 再計算）。Sky globals 配布・夕方→夜の Sun 減衰・Ambient 昼夜補間・時刻連動 fog dusk 寄せを追加。
- `CloudSeaLayer.cs` — `followCameraXZ`（既定 ON）+ `recenterThreshold`。Y は維持し XZ をカメラ追従。間引き比較で毎フレーム書換えを回避。
- `SandboxBootstrap.cs` — `autoAttachSpawners` に `DayNightCycle`/`ProceduralSky`/`VolumeProfileSetup`/`SummitVisualEffects` を追加。

**検証（Sandbox.unity Play）**:
- 6 コンポーネント全 attached ✓（Atmos/Cycle/Sky/Vol/Cloud/Summit）
- Skybox = `Sandbox/ProceduralGradientSky` 差替成功 ✓。SkyZenith/Horizon/SunDir が時刻に応じ配布 ✓
- Volume profile `SandboxVolumeProfile` に Tonemapping/ColorAdjustments/Bloom/Vignette 全 active ✓
- CloudSea Y=47.6・カメラ XZ 追従発火 ✓ / SummitBeam at Y≈119（summit 一致）+ Particles 生成 ✓
- TimeOfDay 進行中（0.423 観測） / Camera farClip=1800 / Fog Linear 180-1200 ✓
- 全シェーダー supported（ProceduralGradientSky/CloudSeaSoft/URP Unlit）✓ / Console Error 0 ✓

**Step 5 で残した暫定**:
- 雲海はまだ 1 枚 plane（カメラ追従のみ）。多層化 / Volumetric は将来。
- 山頂演出は常時表示の環境演出。Trigger 連動（カメラリフト・達成 SE）は別 Step（単機ポリッシュ）。
- DayNightCycle は既定 loop ON（dayLength=600s）。シーン固定時刻にしたい場合は Inspector で `enableCycle=OFF`。

### PEAK 級改修 Step 4（2026-05-29 追加・検証済み）
バーティカリティ & 環境帯。標高で色が変わる地形・中腹の雲海・距離フォグ・遠景視認性。

**新規ファイル**:
- `Assets/Sandbox/Script/World/Environment/AtmosphericProfileController.cs` — Shader Global で標高帯のしきい値/色を配布、`RenderSettings.fog*` を設定、カメラ高度で fog 色を補間、Sun の色温度/角度/強度 + Ambient(Trilight) を設定。`Camera.main.farClipPlane` を 1800 に拡張。`[DefaultExecutionOrder(-25)]`
- `Assets/Sandbox/Script/World/Environment/CloudSeaLayer.cs` — 全コライダーベイク完了後に `summit.Y * 0.40` 高度へ 2200m 大の Quad を配置、`Sandbox/CloudSeaSoft` マテリアル + 円形 alpha falloff + 緩慢スクロール noise
- `Assets/Sandbox/Script/World/Environment/Shaders/CloudSeaSoft.shader` — URP 透過 Unlit。Hash+Value noise を `_Time.y * _ScrollSpeed` で流す、`_RadiusFalloff` で中心→端の alpha 減衰

**変更**:
- `Assets/Sandbox/Script/World/Generation/Base/Shaders/TerrainBiomeSampled.shader` — Forward pass に **標高帯 overlay** を追加。世界 Y で 4 帯（浅瀬→草→岩→雪）の smoothstep 重みを `_ColShore/_ColGrass/_ColRock/_ColSnow` でブレンドし `_BandStrength` で biome 色と lerp。Shader Globals: `_ShoreLine/_ShoreBlend/_GrassLine/_GrassBlend/_RockLine/_RockBlend/_SnowLine/_SnowBlend/_BandStrength`
- `SandboxGrappableHints.cs` — `hintsPerNode 3→1`, `difficultSlopeDeg 35→40`, `maxHints=60`, `minSpacing=8m` を追加。最小距離フィルタで密集回避（135→19 に削減）
- `SandboxBootstrap.cs` — `autoAttachSpawners` に `AtmosphericProfileController`/`CloudSeaLayer` を追加。`using Sandbox.World.Environment;` import

**検証（Sandbox.unity Play）**:
- AtmosphericProfileController + CloudSeaLayer ともに attached ✓
- 雲海 Quad placed at Y=47.6 (summit 119 × 0.40) ✓
- GrappableHints **19** (was 135、目標 40 以下) ✓
- Fog enabled, Linear, 180-1200m, カメラ高度で `fogColor` 補間 ✓
- Shader Globals: snow=85, rock=55, grass=18, shore=4, strength=0.65 ✓
- Camera farClip=1800 → 山頂遠望可能 ✓
- 全シェーダー supported ✓
- Console Error/Warning 0 件 ✓

**Step 4 で残した暫定**:
- Skybox は標準のまま — 時刻演出は Sun 色/角度の補間 + Ambient(Trilight) のみ。プロシージャル sky への置換は Step 5/規約緩和後のアセット導入時に。
- 雲海は 1 枚 plane（カメラ中心配置）。Step 5 で カメラ追従 / cylindrical / Volumetric 化を検討。
- 標高帯の数値は仮チューニング（shore=4/grass=18/rock=55/snow=85）。実プレイで微調整するべし。
- 朝焼け→正午の `timeOfDay` は Inspector の固定値（既定 0.4）。動的サイクル化は Step 5 以降。

### PEAK 級改修 Step 3（2026-05-29 追加・検証済み）
ルート/登攀性を curated 風に成立させ、player rig をフル機能化。

**新規ファイル**:
- `Assets/Sandbox/Script/World/Generation/Route/RouteNode.cs` — Pos/SlopeDeg/SegmentDifficulty を持つ readonly struct
- `Assets/Sandbox/Script/World/Generation/Route/RouteGraphGenerator.cs` — ルート生成。**v1 は spawn→summit XZ 直線 + 地形 Y スナップ**（Ridged MF 山岳では尖った peak への gentle 道が無く A* は「水平移動→終端ジャンプ」になるため、curated 性を明示する直線方式を採用）。A* バリアントは `GenerateAStar` として private 保持（将来の改良用に MinHeap/SnapToNearestValid/SimplifyCollinear 同梱）
- `Assets/Sandbox/Script/World/Integration/SandboxRoutePath.cs` — ルート保持 + `LineRenderer`（slope に応じた緑→赤 gradient）+ `SampleAtFraction(t)`（累積距離）/ `SampleAtAltitudeFraction(t)`（標高 fraction）/ `EnumerateDifficultIndices(thresholdDeg)` API
- `Assets/Sandbox/Script/World/Integration/SandboxGrappableHints.cs` — RoutePath の難所 node（slope ≥ 35°）周辺に決定論的 RNG で Grappable プリミティブを散布。Inspector の `rockPrefab` を assign すれば外部メッシュに差し替え可
- `Assets/Sandbox/Script/World/Integration/SandboxPlayerRigUpgrade.cs` — `Spawned` 後に `PlayerStateManager`/`FirstPersonLook`/`RopeSystem`/`GrappleHook`/`PlayerInputController` を順次 AddComponent し Player タグ + CursorLock 設定

**変更**:
- `SandboxCheckpointBaker.cs` — `SandboxRoutePath.SampleAtAltitudeFraction(f)` でルート上の標高 fraction から XZ を取得（距離 fraction より「登山らしい」分散）。RoutePath 不在時は spawn→summit lerp に fallback
- `SandboxBootstrap.cs` — `routeGridResolution`/`routeSlopeFactor`/`routeMaxClimbableSlope` の Inspector フィールドを追加。`IsAllBaked(1)` かつ `GlobalMaxY != -∞` で一度だけ `RouteGraphGenerator.Generate` を起動して `RoutePath.SetRoute(...)`。自動 AddComponent に `SandboxRoutePath`/`SandboxGrappableHints`/`SandboxPlayerRigUpgrade` を追加

**検証（Sandbox.unity Play）**:
- Route 64 nodes, Y=[6.2,119.3] climb=113m maxSlope=81° — start から summit まで **Y 単調増加**
- Y サンプル(0/25/50/75/100%): 9.1 / 14.9 / 37.5 / 75.1 / 119.3 ← 段階的に上昇
- CP 配置（標高 fraction ベース）: CP1 Y=39.0 / CP2 Y=65.3 / CP3 Y=96.7 — 全 3 つが「中腹〜山頂手前」に分散
- GrappableHints=135 個（slope ≥35° のノード × 3）— 急斜面に密配置
- Player rig: 全コンポーネント (FirstPersonLook/Input/State/Rope/Grapple) attached, tag=Player, CursorLocked
- Console Error/Warning 0 件

**Step 3 で残した暫定**:
- Route は単純な XZ 直線 — Step 4 でセルパン (switchback) や複数ルート (メイン/隠し/上級) 拡張余地
- GrappableHints=135 は密すぎ・視覚的に整理したい — Step 4 で間引き or 質感統一
- Player は Capsule プリミティブ — Step 4 / 規約緩和後の実アート差し替え対象
- `Grappable` タグの存在確認は未テスト（プロジェクトに既存）
- Route の終端 Y は実 summit Y で上書きしているが、中間ノードは terrain 依存のため Y=119 への最終ステップが急（slope 81°）— 視覚的補正は Step 4 で

### PEAK 級改修 Step 2（2026-05-29 追加・検証済み）
A 系統地形上で **歩ける** ようにする基盤統合。Sandbox.unity に統合（Mountain01 は触らず）。

**新規ファイル**:
- `Assets/Sandbox/Script/World/ChunkColliderBaker.cs` — Ready チャンクで HeightTex を AsyncGPUReadback → mesh 生成 → `Awaitable.BackgroundThreadAsync` で `Physics.BakeMesh` → MainThread で `MeshCollider.sharedMesh` 割当。LOD0 解像度（N=129）。エビクト時破棄。`LastReadyCount`/`BakedCount`/`IsAllBaked(min)`/`GlobalMaxY`/`GlobalMaxPos` を公開。
- `Assets/Sandbox/Script/World/Integration/SandboxBootstrap.cs` — TerrainGenerator と同 GameObject に置く統合エントリ。`ChunkColliderBaker` を Update で駆動、`PlayerSpawner`/`SummitGoal`/`CheckpointBaker` を自動 AddComponent。
- `Assets/Sandbox/Script/World/Integration/SandboxPlayerSpawner.cs` — `IsAllBaked` 観測後、`spawnXZ` 下方 Raycast で地面 Y を取り、Player rig を有効化。リグ未指定なら Capsule + Rigidbody + CapsuleCollider + `PlayerMovement` + Camera の最小リグを自動生成。
- `Assets/Sandbox/Script/World/Integration/SandboxSummitGoal.cs` — 観測した最高点に Sphere トリガー + 既存 `SummitGoal` をアタッチ（OnTriggerEnter で Player タグ判定 → クリア処理を流用）。
- `Assets/Sandbox/Script/World/Integration/SandboxCheckpointBaker.cs` — spawn→summit XZ ライン上の 25/50/75% を一度だけキャッシュ → 各点で下方 Raycast → Sphere トリガー + `CheckpointTrigger` 生成 → `CheckpointSystem.Instance.RegisterCheckpoint(...)`。

**変更**:
- `TerrainGenerator.cs` — `public ChunkManager Manager`、`public float ChunkWorldSize` ゲッタ公開。
- `Packages/manifest.json` — `com.unity.transport 2.4.0` 追加。

**検証（Sandbox.unity Play）**:
- 9/9 chunks Ready, ColliderBaker LastReady=9 / Baked=9 / IsAllBaked=True
- Player スポーン (132,5.64,129) / 下方に `ChunkCollider_0_0` （地形 MeshCollider）あり = 歩行可
- Summit `(202, 118.92, 0)` に発光トリガー、既存 `SummitGoal` を attached
- CP1/CP2/CP3 = `(146.5, 15.54, 96.0)` / `(165, 35.46, 64)` / `(183.5, 72.59, 32)` — spawn→summit 直線上の 25/50/75% を地形高度に追従して配置（高度 5→15→35→72→119 で漸増）
- Console Error 0 件、Sandbox 由来 Warning 0 件

**Step 2 で残した暫定**:
- Player rig が最小（FirstPersonLook/GrappleHook/RopeSystem 等は未取付）— Step 3 で取付予定（ロープ系も既存スクリプトを A 側でテスト）
- CP/Summit 配置は spawn→summit 直線の暫定。Step 3 で `RouteGraphGenerator` ベースの curated 導線に置換予定
- コライダーは LOD0 と同解像度 — Step 5 で粗いコリジョン mesh への分離検討
- `com.unity.transport` 取り込み済みだが NetworkManager 等シーン構築は Step 6（Co-op）まで未着手


- **個別インスタンスの距離カリング/ディザフェード**: `PlacementInstancedIndirect.shader` の Forward/ShadowCaster 両 pass の `setup()` で `distance(_WorldSpaceCameraPos, inst.position) > _CullDistance` を満たすインスタンスを scale=0 で退化（描画/影とも消滅）。Forward frag では `_FadeStart`→`_CullDistance` の帯を IGN ディザ + `clip` で徐々に間引き。`PlacementIndirectRenderer.CullDistance`/`FadeStart` を public プロパティで公開、`DrawProto` で MPB に毎フレームセット。`TerrainGenerator` に Inspector フィールド `placementCullDistance`/`placementFadeStart`（既定 300/220）追加。
- **追加ライト/GI**: 両 Forward に `_ADDITIONAL_LIGHTS` / `_ADDITIONAL_LIGHT_SHADOWS` / `_SHADOWS_SOFT` の multi_compile を追加し `GetAdditionalLightsCount()` + `GetAdditionalLight()` ループ。GI は `SampleSH(n)` の SH ambient を継続。
- **地形 per-pixel 法線テクスチャ**: `TerrainBiomeSampled.shader` が `_NormalSlopeTex.xyz`（既存の world normal）を UV サンプルし per-pixel 法線として使用（未バインド時は mesh normal フォールバック）。これで LOD 間引きしても陰影が劣化しない。`TerrainDebugMeshBaker` の MPB で per-chunk バインド。
- **NGO 地形スコープ統合**: 新規 `NetworkedTerrainSeed.cs` = NetworkBehaviour + `NetworkVariable<uint>` でサーバ→全クライアントへ `worldSeed` 同期、`TerrainGenerator.ApplyWorldSeed(uint)` を駆動。`TerrainGenerator` を `BuildPipeline(uint)`/`DisposePipeline()`/`ApplyWorldSeed(uint)` に分解、`deferBuildToNetworkSeed` フラグでネット時はシード到着まで生成を待機（既定 OFF＝シングルプレイ挙動不変）。**シーン側（NetworkManager・Transport・ホスト/参加 UI・プレイヤースポーン・同 GameObject への NetworkObject + NetworkedTerrainSeed 配置・`deferBuildToNetworkSeed=ON` 切替）は別途必要**。`com.unity.netcode.gameobjects 2.11.0` は導入済みだが **`com.unity.transport` は未導入**（実マルチで必要）。
- **実アート導入**: プロジェクト規約「外部アセット不要」によりエージェントからの導入不可。差し替えポイントは `TerrainGenerator` Inspector の placementTree/RockMesh・Material・debugMeshMaterial。
- 検証（実行時と同じ production 経路）: 両シェーダー passCount=2/supported、新プロパティ全部 True、`ApplyWorldSeed(UInt32)`/`NetworkedTerrainSeed isNetworkBehaviour=True` を反射で確認。統合 Play で 9/9 chunks Ready、実行時エラー 0、`placement drawn=6 culled=3` でカリング動作、`CullDistance=300 FadeStart=220` が renderer に伝播。

---

## どこまで終わったか（最終 Todo 状態）

完了:
- [x] IErosionPass interface
- [x] ErosionParams ScriptableObject
- [x] HydraulicErosion.compute（Pack/Droplet/Thermal/Unpack/Normal）
- [x] HydraulicThermalErosionGPU C# ラッパー
- [x] ChunkBufferSet に HeightFixed バッファ追加
- [x] ChunkManager の状態機械に浸食を統合
- [x] TerrainGenerator に浸食設定を追加
- [x] MeshBaker を Ready 状態でベイクするよう更新

## 続きの作業（ここから再開）

Module 2 の残2タスクは **2026-05-29 に両方完了・検証済み**。

1. **【完了】erosion 発散の修正**
   - `HydraulicErosion.compute` に `HEIGHT_SCALE` 正規化（Pack/Unpack/ReadH/Thermal）
     + `MAX_SPEED` クランプ（Droplet 速度更新）が実装済み。`_ErodeMisc` で
     `heightNormalization`(=256) / `maxSpeed`(=6) を渡す配線も整合確認済み。
2. **【完了】Play 相当の浸食検証**
   - `Unity_RunCommand` で RidgedMF 生成 → 浸食 → HeightTex を AsyncGPUReadback で統計。
   - 結果: PRE min/max/mean = -0.31 / 150.10 / 22.06、POST = -13.55 / 147.25 / 10.95、
     NaN/Inf = 0。発散なし・正規化バンド(256m)内に収束で**安定**。コンパイルもクリーン。

> ⚠️ 完成済みだが **ユーザー意向で git 未コミットのまま**。コミットする場合は
> `Assets/Sandbox/Script/World/` 一式 + `Assets/Sandbox/Data/ErosionParams.asset`・
> `RidgedMFParams.asset` のみにスコープを絞る（.DS_Store / com.unity.ai.assistant /
> GDD_ccc.md 等の無関係な未追跡ファイルを巻き込まない）。

## Module 3 検証結果（2026-05-29・RunCommand = 実行時と同じ production コード経路）
- **時分割**: `Caps.SupportsTimeSlicing=True`。budget=4 で 14 dispatch を 4 step に分割完了。POST 統計は一括版と同レンジ（NaN/Inf=0）でリグレッション無し。
- **Biome**: BiomeMaskTex 分類、index 全て範囲内（outOfRange=0）。分布 Water11/Sand54/Grass12/Forest0.1/Rock22/Snow0%。※しきい値は地形に対し Sand 偏重ぎみ、`BiomeParams.asset` で調整可。
- **Placement**: scatter count=326（cap4096・オーバーフロー無し）、trees113/rocks213、badProto=0/outOfBounds=0、Y∈[3.37,92.95] で地表面上。
- **統合 Play**: シーンの `TerrainGenerator` に4参照を配線・保存し Play 実行 → 9 チャンク（loadRadius=1）全てが時分割で Ready 到達・デバッグメッシュ生成。実行時エラー無し。
  - ⚠️ MCP の Camera/SceneView キャプチャは Play 中/URP 環境で失敗（"Failed to render scene preview"）。目視はユーザーが Game ビューで確認する想定。

## 既知の調整事項 / 次フェーズ候補
- ※ Module 3 + 改良4弾（indirect instancing / BiomeParams チューニング / time-slice 細粒度化 / インスタンスライティング / 実メッシュ・マテリアル化 / Biome 重みブレンド / シャドウ / 地形 UV サンプル / LOD・カリング / 個別インスタンスのカリング+フェード / 追加ライト+GI / 地形 per-pixel 法線 / NGO シード同期）は **2026-05-29 完了**（上記セクション参照）。**主要機能は一通り実装完了**。
- LOD は近距離(loadRadius=1)では全 LOD0 のため実描画で間引きは未発火（コードは検証済み・決定論的）。LOD 発火は viewer 距離 > lod1Distance(400) で確認可能。
- **残作業候補（任意・要追加要望）**:
  - **NGO シーン側構築**: `com.unity.transport` パッケージ導入 → シーンに NetworkManager + UnityTransport + ホスト/参加 UI + プレイヤースポーン + 同 GameObject に NetworkObject + NetworkedTerrainSeed 配置 + `deferBuildToNetworkSeed=ON`。
  - **scene の TerrainGenerator** には今日追加の 3 フィールド（`placementCullDistance`/`placementFadeStart`/`deferBuildToNetworkSeed`）が未シリアライズ（C# 既定値 300/220/false で稼働中・Play 検証済）。値を変更する場合は Inspector で編集 → シーン保存。
  - **実アート差し替え**: 規約により外部アセットの導入はエージェントから不可。Inspector の placementTree/RockMesh・Material・debugMeshMaterial に任意のアセットを割当。
  - 軽微なクリーンアップ候補: `TerrainBiomeSampled.shader` の未使用 `_BiomeColorTex_ST`（手動 UV リマップに置換済みなので削除可・harmless）。

> ⚠️ 全て **git 未コミット**（ユーザー意向）。コミット時は `Assets/Sandbox/Script/World/` 一式 +
> `Sandbox/Data/{ErosionParams,RidgedMFParams,BiomeParams,PlacementParams}.asset` + `Sandbox/Scene/Sandbox.unity`
> にスコープを絞る（.DS_Store / com.unity.ai.assistant / GDD_ccc.md 等の無関係ファイルを巻き込まない）。

---

## 再開時の最初の一手（推奨手順）
1. Unity MCP は再コンパイル/ドメインリロード中はハング・切断する（[[unity-mcp-recompile-hang]]）。アセット生成と検証 RunCommand は分離する。
2. `Unity_ReadConsole`(Error) で 0 を確認。
3. 上記「次フェーズ候補」から着手。各パスは `Unity_RunCommand` で単体検証可能（Module 1-3 と同手法）。
