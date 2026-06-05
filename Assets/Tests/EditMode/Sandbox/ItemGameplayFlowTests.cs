using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode: shop rope connect/disconnect flows
public class ItemGameplayFlowTests
{
    [TearDown]
    public void TearDownRopeServices()
    {
        ResetRopeManagerForEditModeTest();
    }

    [Test]
    public void Stretcher_TryToggleExpand_changes_collider_size()
    {
        var go = CreateStretcher();
        var stretcher = go.GetComponent<StretcherItem>();
        var col = go.GetComponent<BoxCollider>();

        Assert.IsTrue(stretcher.TryToggleExpand());
        Assert.Less(col.size.x, 1f, "折畳時はコンパクト");

        Assert.IsTrue(stretcher.TryToggleExpand());
        Assert.Greater(col.size.x, 1.5f, "展開時は担架サイズ");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Winch_DeployFromHand_requires_hand_item()
    {
        var player = CreatePlayerWithInventory();
        var inv = player.GetComponent<PlayerInventory>();

        var winchGo = CreateWinch();
        var winch = winchGo.GetComponent<PortableWinchItem>();

        Assert.IsFalse(winch.TryDeployFromHand(inv, player.transform));

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(winchGo);
    }

    [Test]
    public void Winch_DeployFromHand_succeeds_on_flat_ground()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = Vector3.one * 5f;

        var player = CreatePlayerWithInventory();
        player.transform.position = new Vector3(0f, 1f, 0f);
        var inv = player.GetComponent<PlayerInventory>();

        var winchGo = CreateWinch();
        var winch = winchGo.GetComponent<PortableWinchItem>();
        PrepareItem(winch);
        Assert.IsTrue(inv.TryEquipHand(winch));

        Physics.SyncTransforms();
        Assert.IsTrue(winch.TryDeployFromHand(inv, player.transform));
        Assert.IsTrue(winch.IsDeployedInWorld);
        Assert.IsFalse(inv.HasHandItem);

        Object.DestroyImmediate(player);
        Object.DestroyImmediate(winchGo);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void IceAxe_PlaceGripPoint_does_not_consume_durability()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.AddComponent<IceAxeItem>();
        var axe = go.GetComponent<IceAxeItem>();
        float before = axe.DurabilityPct;

        Assert.IsTrue(axe.PlaceGripPoint(Vector3.up * 2f, Vector3.forward));
        Assert.AreEqual(before, axe.DurabilityPct, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GrapplingHook_TryRecover_requires_proximity()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.AddComponent<GrapplingHookItem>();
        var hook = go.GetComponent<GrapplingHookItem>();

        Assert.IsFalse(hook.TryRecover(Vector3.zero));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ThermalCase_toggle_protect_and_stop()
    {
        var caseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        caseGo.AddComponent<ThermalCaseItem>();
        var thermal = caseGo.GetComponent<ThermalCaseItem>();

        var relicGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        relicGo.AddComponent<Rigidbody>();
        var relic = relicGo.AddComponent<CrystalCupRelic>();
        InvokeAwake(relic);

        Assert.IsTrue(thermal.TryProtectRelic(relic));
        Assert.IsTrue(thermal.IsProtecting);

        thermal.StopProtecting();
        Assert.IsFalse(thermal.IsProtecting);

        Object.DestroyImmediate(caseGo);
        Object.DestroyImmediate(relicGo);
    }

    [Test]
    public void SecureBelt_toggle_strap_and_unstrap()
    {
        var beltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beltGo.AddComponent<SecureBeltItem>();
        var belt = beltGo.GetComponent<SecureBeltItem>();

        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;

        var relicGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        relicGo.AddComponent<Rigidbody>();
        var relic = relicGo.AddComponent<CrystalCupRelic>();
        InvokeAwake(relic);
        var carrier = relicGo.AddComponent<RelicCarrier>();
        InvokeAwake(carrier);
        carrier.PickUp(player.transform, 0);

        Assert.IsTrue(belt.TryStrap(relic, player.transform));
        Assert.IsTrue(belt.IsStrapped);

        belt.Unstrap();
        Assert.IsFalse(belt.IsStrapped);

        Object.DestroyImmediate(beltGo);
        Object.DestroyImmediate(relicGo);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void Winch_cable_flow_deploy_attach_reel()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = Vector3.one * 5f;

        var player = CreatePlayerWithInventory();
        player.transform.position = new Vector3(0f, 1f, 0f);
        var inv = player.GetComponent<PlayerInventory>();

        var winchGo = CreateWinch();
        var winch = winchGo.GetComponent<PortableWinchItem>();
        PrepareItem(winch);
        Assert.IsTrue(inv.TryEquipHand(winch));
        Physics.SyncTransforms();
        Assert.IsTrue(winch.TryDeployFromHand(inv, player.transform));

        Assert.IsTrue(winch.TryDeployCable());
        Assert.IsTrue(winch.HasCableHook);

        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.AddComponent<Rigidbody>();
        Assert.IsTrue(winch.TryAttachCableTo(target.GetComponent<Rigidbody>()));
        Assert.IsTrue(winch.IsCableAttached);

        Assert.IsTrue(winch.TryToggleReel(player.transform));
        Assert.IsTrue(winch.IsReeling);

        Object.DestroyImmediate(target);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(winchGo);
        Object.DestroyImmediate(ground);
    }

    [Test]
    public void LongRope_AttachToRelic_connects_player_and_relic()
    {
        var (rmGo, rm) = CreateTestRopeManager();

        var player = CreatePlayerWithInventory();
        var inv = player.GetComponent<PlayerInventory>();

        var ropeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ropeGo.AddComponent<Rigidbody>();
        ropeGo.AddComponent<LongRopeItem>();
        var longRope = ropeGo.GetComponent<LongRopeItem>();
        PrepareItem(longRope);
        Assert.IsTrue(inv.TryEquipHand(longRope));

        rm.RegisterPlayer(1, player.GetComponent<Rigidbody>());

        var relicGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        relicGo.AddComponent<Rigidbody>();
        var relic = relicGo.AddComponent<CrystalCupRelic>();
        InvokeAwake(relic);
        relicGo.AddComponent<RelicCarrier>();
        relicGo.AddComponent<RelicGrabPoint>();
        relicGo.transform.position = player.transform.position + Vector3.forward * 1.5f;

        Assert.AreSame(rm, GameServices.Ropes);
        Assert.IsTrue(longRope.TryAttachToRelic(relic, 1, player.transform.position));
        Assert.IsTrue(longRope.IsRelicMode);
        Assert.IsTrue(rm.IsPlayerConnectedToRelic(1));

        var ropeSystem = rmGo.GetComponentInChildren<PlayerRopeSystem>(true);
        Assert.IsNotNull(ropeSystem);
        Assert.GreaterOrEqual(ShopRopeConstants.LongRopeBreakForce, 2500f);
        Assert.GreaterOrEqual(ShopRopeConstants.ShortRopeBreakForce, 3000f);

        longRope.CutRope();
        Assert.IsFalse(rm.IsPlayerConnectedToRelic(1));

        Object.DestroyImmediate(rmGo);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ropeGo);
        Object.DestroyImmediate(relicGo);
    }

    [Test]
    public void ShortRope_ConnectToPlayer_uses_shop_constants()
    {
        var (rmGo, rm) = CreateTestRopeManager();

        var playerA = CreatePlayerWithInventory();
        var playerB = CreatePlayerWithInventory();
        playerB.transform.position = playerA.transform.position + Vector3.right * 1.5f;

        rm.RegisterPlayer(1, playerA.GetComponent<Rigidbody>());
        rm.RegisterPlayer(2, playerB.GetComponent<Rigidbody>());

        var ropeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ropeGo.AddComponent<Rigidbody>();
        ropeGo.AddComponent<ShortRopeItem>();
        var shortRope = ropeGo.GetComponent<ShortRopeItem>();
        PrepareItem(shortRope);

        Assert.AreSame(rm, GameServices.Ropes);
        Assert.AreEqual(ShopRopeConstants.ShortRopeBreakForce, shortRope.BreakForce);
        Assert.IsTrue(shortRope.TryConnectToPlayer(1, 2));
        Assert.IsTrue(shortRope.IsConnected);

        shortRope.CutRopeLocalOnly();
        Assert.IsFalse(shortRope.IsConnected);

        Object.DestroyImmediate(rmGo);
        Object.DestroyImmediate(playerA);
        Object.DestroyImmediate(playerB);
        Object.DestroyImmediate(ropeGo);
    }

    [Test]
    public void ShortRope_ConnectToAnchor_registers_player_anchor_rope()
    {
        var (rmGo, rm) = CreateTestRopeManager();

        var player = CreatePlayerWithInventory();
        rm.RegisterPlayer(1, player.GetComponent<Rigidbody>());

        var anchor = new GameObject("AnchorBolt_Placed");
        anchor.transform.position = player.transform.position + Vector3.forward * 1f;
        anchor.AddComponent<Rigidbody>().isKinematic = true;
        rm.RegisterAnchorPoint(anchor.transform);

        var ropeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ropeGo.AddComponent<Rigidbody>();
        ropeGo.AddComponent<ShortRopeItem>();
        var shortRope = ropeGo.GetComponent<ShortRopeItem>();
        PrepareItem(shortRope);

        Assert.AreSame(rm, GameServices.Ropes);
        Assert.IsTrue(shortRope.TryConnectToAnchor(anchor.transform, 1, player.transform.position));
        Assert.IsTrue(shortRope.IsConnected);
        Assert.IsTrue(rm.IsPlayerConnectedToAnchor(1));

        shortRope.CutRopeLocalOnly();
        Assert.IsFalse(rm.IsPlayerConnectedToAnchor(1));

        Object.DestroyImmediate(rmGo);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(anchor);
        Object.DestroyImmediate(ropeGo);
    }

    [Test]
    public void WinchCableChain_EstimateTension_uses_joint_forces_when_deployed()
    {
        var anchorGo = new GameObject("Anchor");
        anchorGo.transform.position = Vector3.up * 3f;
        anchorGo.AddComponent<Rigidbody>().isKinematic = true;

        var lrGo = new GameObject("Line");
        var lr = lrGo.AddComponent<LineRenderer>();

        var chainGo = new GameObject("Chain");
        var chain = chainGo.AddComponent<WinchCableChain>();
        chain.Configure(anchorGo.transform, lr, 20f, 1.5f);

        Assert.IsTrue(chain.DeployHook(anchorGo.transform.position + Vector3.down * 2f));
        chain.Reel(0.5f);
        Assert.GreaterOrEqual(chain.EstimateTension(), 0f);

        Object.DestroyImmediate(anchorGo);
        Object.DestroyImmediate(lrGo);
        Object.DestroyImmediate(chainGo);
    }

    private static (GameObject go, RopeManager manager) CreateTestRopeManager()
    {
        ResetRopeManagerForEditModeTest();

        var rmGo = new GameObject("RopeManager_Test");
        var rm   = rmGo.AddComponent<RopeManager>();

        // EditMode では Awake が走らず、シーン内の既存 RopeManager があると
        // Awake 内の重複排除でテスト用インスタンスが Destroy される。登録だけ行う。
        typeof(RopeManager).GetField(
                "_instance",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, rm);
        GameServices.Register(rm);

        return (rmGo, rm);
    }

    private static void ResetRopeManagerForEditModeTest()
    {
        typeof(RopeManager).GetField(
                "_instance",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
        GameServices.ClearRopes();
    }

    private static GameObject CreatePlayerWithInventory()
    {
        var player = new GameObject("Player");
        player.AddComponent<Rigidbody>();

        var handAnchor = new GameObject("HandAnchor");
        handAnchor.transform.SetParent(player.transform, false);

        var inv = player.AddComponent<PlayerInventory>();
        InvokeAwake(inv);

        var handField = typeof(PlayerInventory).GetField(
            "_handAnchor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        handField?.SetValue(inv, handAnchor.transform);

        return player;
    }

    private static void PrepareItem(ItemBase item)
    {
        if (item == null) return;
        InvokeAwake(item);
    }

    private static void InvokeAwake(MonoBehaviour behaviour)
    {
        var awake = behaviour.GetType().GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        awake?.Invoke(behaviour, null);
    }

    private static GameObject CreateStretcher()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.AddComponent<Rigidbody>();
        var stretcher = go.AddComponent<StretcherItem>();
        InvokeAwake(stretcher);
        return go;
    }

    private static GameObject CreateWinch()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.AddComponent<Rigidbody>();
        go.AddComponent<LineRenderer>();
        go.AddComponent<WinchCableChain>();
        go.AddComponent<PortableWinchItem>();
        return go;
    }
}
