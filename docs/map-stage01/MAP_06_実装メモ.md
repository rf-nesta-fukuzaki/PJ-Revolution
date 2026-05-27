# Stage 01 MAP — 実装メモ

## Unity実行環境

この作業環境では Unity 6000.3.8f1 の実行ファイルが見つからない。

確認済み:
- `Unity` / `Unity.exe` はPATH上に存在しない
- 標準インストール先候補から `Unity.exe` を検出できない
- Unity Installer の代表的なレジストリキーからも検出できない

Unity Editorでのシーン生成・PlayMode検証は、Unity環境が入っている別PCで実行する。

## 別PCで実行する作業

1. Unity 6000.3.8f1 でプロジェクトを開く
2. メニューから `Peak Plunder > Stage01 > Build Gameplay Scene` を実行する
3. `Assets/Sandbox/Scenes/Gameplay.unity` にStage01構造が生成されることを確認する
4. PlayModeで登山ルート、SpawnManager、RouteGate、IcePatch、SummitGoalを検証する

## 実装方式

大量のScene YAML手編集は避け、`Assets/Sandbox/Script/Editor/Stage01MapBuilder.cs` のUnity Editor APIでシーンを構築する。
