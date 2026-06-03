using UnityEngine;

/// <summary>
/// <see cref="WireRopeActionController"/> のチューニング値を外部化する ScriptableObject。
///
/// 目的:
///   ・複数プレイヤー（Co-op 1〜4人）が 1 つの asset を共有し「一度調整→全員反映」を実現する
///   ・Play モード中の asset 差し替えでプリセット比較を可能にする
///   ・チューニングの版管理をシーン/コードと分離する
///
/// 取得経路:
///   ・Inspector で割り当てられていればそれを使用
///   ・未割り当て（実行時 AddComponent 時など）は <see cref="LoadDefault"/> が
///     Resources の "WireRopeActionConfig" をロード。無ければコード既定値の実体を生成する
///     （= asset 不在でも従来挙動を保証）。
/// </summary>
[CreateAssetMenu(menuName = "PeakPlunder/WireRopeActionConfig", fileName = "WireRopeActionConfig")]
public sealed class WireRopeActionConfigSO : ScriptableObject
{
    /// <summary>Resources からロードする際の既定パス（拡張子なし）。</summary>
    public const string ResourcePath = "WireRopeActionConfig";

    [Header("投擲")]
    public float MinThrowRange = 4f;
    public float MaxThrowRange = 28f;
    public float ThrowSpeed = 42f;
    [Header("溜め — 頭上ラッソ（キャラ基準）")]
    public float GaugeOscillationSpeed = 55f;
    public float SpinVisualSpeed = 620f;
    [Tooltip("水平面からの仰角[°]。80°でほぼ真上、やや前。")]
    [Range(60f, 89f)]
    public float LassoElevationDeg = 80f;
    [Tooltip("胸元からラッソ中心までの距離[m]。")]
    public float LassoCenterDistance = 1.15f;
    [Tooltip("キャラ足元から胸の高さ[m]。")]
    public float LassoChestHeight = 1.05f;
    public float LassoRadius = 0.95f;
    [Tooltip("手元から輪への接続を太く見せる。")]
    public float LassoHandWidthMul = 1.35f;
    [Header("ロープ表示")]
    public float RopeStartWidth = 0.09f;
    public float RopeEndWidth = 0.05f;
    public float RopeSag = 0.35f;
    public Color RopeColor = new Color(0.92f, 0.78f, 0.42f, 1f);
    [Header("回収 — 張力（物理）")]
    public float PullTensionAccel = 46f;
    [Tooltip("引き方向の最低速度。近づいてもここまで落ちない。")]
    public float PullMinSpeed = 14f;
    public float PullMaxSpeed = 38f;
    [Tooltip("開始時のガキンとした初速（掃除機コンセントを引く反動）。")]
    public float PullSnapImpulse = 17f;
    [Tooltip("ロープ張力で許容する最大仰角[°]（真上への吹き上がり防止）。")]
    [Range(5f, 45f)]
    public float MaxPullElevationDeg = 18f;
    [Tooltip("アンカーが高い壁フック時に許す最大仰角[°]。")]
    [Range(10f, 55f)]
    public float MaxPullElevationDegWallUp = 36f;
    [Tooltip("張力による上向き速度の上限[m/s]。")]
    public float MaxUpwardSpeedFromTension = 12f;
    [Header("回収 — 対象引き寄せ（遺物 / キャラ）")]
    [Tooltip("ロープ先が遺物・キャラの場合、回収で対象を自分へ引き寄せる加速度。")]
    public float TargetPullAccel = 65f;
    [Tooltip("対象を引き寄せるときの最大速度[m/s]。")]
    public float TargetPullMaxSpeed = 24f;
    [Tooltip("対象を引き寄せるときの最低速度[m/s]（近づいても止まらない下限）。")]
    public float TargetPullMinSpeed = 8f;
    [Tooltip("対象がこの距離[m]まで近づいたら回収完了。")]
    public float TargetPullArrivalDistance = 1.5f;
    [Header("バカげ物理 — ロープ弾性（控えめ）")]
    [Tooltip("ロープが伸びた分だけ張力が増える係数。")]
    public float RopeElasticGain = 0.75f;
    [Range(1f, 2.5f)]
    public float RopeElasticPower = 1.35f;
    [Tooltip("速度がロープと大きくずれたときだけアンカーへ寄せる。")]
    public float SwingCentripetalGain = 0.2f;
    [Tooltip("溜めゲージによる張力の振れ幅。")]
    [Range(0f, 0.35f)]
    public float ChargePowerSpread = 0.22f;
    [Tooltip("地面をロープ方向へ滑らせる補助（横方向の謎加速はしない）。")]
    public float GroundSlideAssistAccel = 22f;
    [Tooltip("地面へのめり込み速度を減衰する係数（抵抗感）。")]
    [Range(0.5f, 1f)]
    public float GroundImpactBleed = 0.88f;
    [Tooltip("スライドの横ずれを抑える抵抗。")]
    public float GroundSlideLateralResist = 14f;
    [Tooltip("地面からこれ以上離れていたらスライド補正しない[m]。")]
    public float GroundSlideMaxAirGap = 0.55f;
    [Tooltip("近づくほど張力が増す係数（スナップ感）。")]
    public float PullSnapTensionBoost = 0.68f;
    public float PullPerpendicularDamp = 0.32f;
    [Tooltip("回収中の WASD 追加加速度（Explorer と併用）。")]
    public float RetrieveSteerAccel = 32f;
    [Tooltip("ロープ離脱後に保つ速度の倍率（オーバーシュート）。")]
    [Range(0.8f, 1.3f)]
    public float OvershootMomentumScale = 1.2f;
    [Header("先端到達 — ロープ回収と慣性吹っ飛び")]
    [Tooltip("アンカー（ロープ先端）までの到達判定距離[m]。")]
    public float AnchorArrivalDistance = 1.65f;
    [Tooltip("張力速度に掛ける慣性倍率。")]
    public float ReleaseInertiaScale = 1.15f;
    [Tooltip("離脱時の最低吹っ飛び速度[m/s]。")]
    public float ReleaseInertiaMinSpeed = 9f;
    [Tooltip("離脱時の最高吹っ飛び速度[m/s]。")]
    public float ReleaseInertiaMaxSpeed = 40f;
    [Range(0f, 1f)]
    public float ReleasePerpendicularRetain = 0.2f;
    [Tooltip("離脱後、この距離[m]滑ったら回収終了（入力があれば即終了）。")]
    public float MaxOvershootDistance = 6.5f;
    [Tooltip("離脱後、この速度未満で入力なしのとき回収終了。")]
    public float OvershootEndSpeed = 1.4f;
    public float RetrieveStopDistance = 1.4f;
    public float MaxRetrieveSeconds = 10f;
    public float StandOffFromSurface = 0.75f;
    public float GroundSnapUp = 2.5f;
    [Tooltip("めり込み解消の反復回数。")]
    public int DepenetrateIterations = 20;
    [Tooltip("ゲージが高いほど初動の反動が強い。")]
    public float GaugeLaunchBoost = 8f;
    [Header("遷移の滑らかさ")]
    public float RetrieveTensionRampSeconds = 0.22f;
    [Tooltip("停止点手前で張力を弱め始める距離[m]。")]
    public float ReleaseSoftDistance = 2.1f;
    [Range(0f, 1f)]
    public float RetrieveStartVelocityBlend = 0.58f;
    public float ReleaseVelocityBlend = 0.58f;
    public float PullAxisTurnSpeed = 7.5f;
    public float VisualBlendSpeed = 14f;
    public float MaxSpeedChangePerSecond = 70f;
    [Tooltip("先端付近で速度が落ちたとき、到達扱いで離脱するまでの時間[s]。")]
    public float PullStallReleaseSeconds = 0.45f;
    [Tooltip("オーバーシュートが止まったときの強制終了[s]。")]
    public float OvershootMaxSeconds = 3.8f;
    [Tooltip("張力減衰の下限（0にすると停止点手前で完全停止しやすい）。")]
    [Range(0.2f, 1f)]
    public float TensionFalloffFloor = 0.52f;
    [Header("回収速度 — 射程・ゲージ連動スケール")]
    [Tooltip("最小射程付近での目標引き速度[m/s]。近距離はここまで穏やかに引く。")]
    public float PullSpeedShort = 16f;
    [Tooltip("目標速度に対する最低維持速度の割合（近づいても止まらない下限）。")]
    [Range(0.3f, 0.9f)]
    public float PullFloorFraction = 0.55f;
    [Tooltip("溜めゲージによる引き速度の振れ幅（±割合）。0.35 = 弱溜め×0.65 / 強溜め×1.35。")]
    [Range(0f, 0.6f)]
    public float PullSpeedChargeSpread = 0.35f;
    [Tooltip("オーバーシュート距離 = 開始時ロープ長 × この係数（上限は MaxOvershootDistance）。")]
    public float OvershootDistanceFactor = 0.32f;
    [Tooltip("オーバーシュート距離の下限[m]。")]
    public float OvershootMinDistance = 1f;
    [Tooltip("地面アンカーがこの高さ[m]以上上方なら登攀とみなし 3D 距離で到達判定し、坂を登り切る。")]
    public float GroundClimbLiftThreshold = 1.5f;
    [Header("衝突 — アンカー方向スリングショット")]
    [Tooltip("障害物に当たったとき、アンカー（ロープ先端）へ向かう初速[m/s]。")]
    public float ImpactSlingshotSpeed = 34f;
    [Tooltip("衝突後の速度倍率（1超えでバカげ反発）。")]
    [Range(1f, 1.35f)]
    public float ImpactRestitution = 1.1f;
    [Tooltip("床へ強くめり込んだときだけ付与する小さな跳ね上げ。")]
    public float ImpactSlingshotFloorPopUp = 3.5f;
    [Tooltip("衝突直前の速度をどれだけ加算するか。")]
    [Range(0f, 1f)]
    public float ImpactSlingshotCarryFactor = 0.4f;
    public float ImpactSlingshotMaxSpeed = 52f;
    public float ImpactSlingshotBoostSeconds = 1.2f;
    public float ImpactSlingshotCooldown = 0.14f;
    [Tooltip("接触面へのめり込み速度がこの値以上ならスリングショット発動。")]
    public float ImpactSlingshotMinIntoSpeed = 2f;
    [Tooltip("強い衝突とみなすめり込み速度。")]
    public float ImpactSlingshotHardImpactSpeed = 5f;
    [Tooltip("壁・岩など（法線の Y がこれ未満）への接触で発動。")]
    [Range(0.2f, 0.8f)]
    public float ObstacleNormalMaxY = 0.55f;
    [Tooltip("床へのめり込み速度がこれ以上ならアンカー方向へ弾く。")]
    public float FloorSlingshotMinIntoSpeed = 1.2f;
    [Tooltip("床を速く滑っているときもスリングショットする水平速度（高め＝誤発動しにくい）。")]
    public float FloorSlingshotMinHorizSpeed = 9f;
    [Tooltip("めり込みがこれ未満なら位置スナップせず速度だけ整える。")]
    public float SoftGroundPenetrationDepth = 0.35f;
    [Header("フィール / 演出")]
    public float ThrowEasePower = 1.85f;
    public float TensionSoundInterval = 0.38f;
    public float TraumaRetrieveStart = 0.14f;
    public float TraumaRopeHit = 0.22f;
    public float TraumaRopeRelease = 0.2f;
    public float TraumaImpactSlingshot = 0.32f;

    /// <summary>
    /// 起動時の事前条件チェック（DbC）。値域の不正な組み合わせを開発ビルドで即検出する。
    /// <see cref="Contract.Requires"/> は UNITY_ASSERTIONS 無効時（リリース）には除去される。
    /// </summary>
    public void Validate()
    {
        Contract.Requires(MinThrowRange < MaxThrowRange,
            $"MinThrowRange({MinThrowRange}) < MaxThrowRange({MaxThrowRange}) であること");
        Contract.Requires(ThrowSpeed > 0f, "ThrowSpeed は正であること");
        Contract.Requires(PullMinSpeed <= PullMaxSpeed,
            $"PullMinSpeed({PullMinSpeed}) <= PullMaxSpeed({PullMaxSpeed}) であること");
        Contract.Requires(TargetPullMinSpeed <= TargetPullMaxSpeed,
            $"TargetPullMinSpeed({TargetPullMinSpeed}) <= TargetPullMaxSpeed({TargetPullMaxSpeed}) であること");
        Contract.Requires(ReleaseInertiaMinSpeed <= ReleaseInertiaMaxSpeed,
            $"ReleaseInertiaMinSpeed({ReleaseInertiaMinSpeed}) <= ReleaseInertiaMaxSpeed({ReleaseInertiaMaxSpeed}) であること");
        Contract.Requires(OvershootMinDistance <= MaxOvershootDistance,
            $"OvershootMinDistance({OvershootMinDistance}) <= MaxOvershootDistance({MaxOvershootDistance}) であること");
        Contract.Requires(DepenetrateIterations > 0, "DepenetrateIterations は 1 以上であること");
        Contract.Requires(MaxRetrieveSeconds > 0f, "MaxRetrieveSeconds は正であること");
    }

    /// <summary>
    /// 既定の設定を取得する。Resources に <see cref="ResourcePath"/> の asset があればそれを共有し、
    /// 無ければコード既定値の実体を生成する（asset 不在でも従来挙動を保証）。
    /// </summary>
    public static WireRopeActionConfigSO LoadDefault()
    {
        var shared = Resources.Load<WireRopeActionConfigSO>(ResourcePath);
        return shared != null ? shared : CreateInstance<WireRopeActionConfigSO>();
    }
}
